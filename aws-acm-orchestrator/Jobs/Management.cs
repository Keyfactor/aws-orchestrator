// Copyright 2025 Keyfactor
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and 
// limitations under the License.

using Amazon.CertificateManager;
using Amazon.CertificateManager.Model;
using Amazon.Runtime.Internal.Util;
using Keyfactor.Extensions.Aws;
using Keyfactor.Extensions.Aws.Models;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Keyfactor.Extensions.Orchestrator.Aws.Acm.Jobs
{
    public class Management : IManagementJobExtension
    {
        public string ExtensionName => "AWS-ACM-v3";

        private static String certStart = "-----BEGIN CERTIFICATE-----\n";
        private static String certEnd = "\n-----END CERTIFICATE-----";

        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        private static Func<String, String> _pemify = (ss => ss.Length <= 64 ? ss : ss.Substring(0, 64) + "\n" + _pemify(ss.Substring(64)));

        internal IAmazonCertificateManager AcmClient;
        internal ILogger Logger;
        internal IPAMSecretResolver PamSecretResolver;

        internal AwsAuthUtility AuthUtilities;

        public Management(IPAMSecretResolver pam, ILogger<Management> logger)
        {
            PamSecretResolver = pam;
            Logger = logger;
            AuthUtilities = new AwsAuthUtility(pam);
        }

        public JobResult ProcessJob(ManagementJobConfiguration jobConfiguration)
        {
            Logger.MethodEntry();

            Logger.LogTrace("Deserializing Store Properties to AuthCustomFieldParameters object.");
            AuthCustomFieldParameters customFields = JsonConvert.DeserializeObject<AuthCustomFieldParameters>(jobConfiguration.CertificateStoreDetails.Properties,
                    new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });
            Logger.LogTrace("Deserialized Store Properties.");

            // StorePath may be a legacy region ("us-east-1") with the Role ARN in ClientMachine, or
            // a self-contained "<roleArn>|<region>" emitted by cross-account Discovery. Parse handles both.
            var (roleArn, region) = StorePathParser.Parse(
                jobConfiguration.CertificateStoreDetails.StorePath,
                jobConfiguration.CertificateStoreDetails.ClientMachine);

            string storeRef = Inventory.StoreRef(jobConfiguration.CertificateStoreDetails.ClientMachine,
                jobConfiguration.CertificateStoreDetails.StorePath);

            AuthenticationParameters authParams = new AuthenticationParameters
            {
                RoleARN = roleArn,
                Region = region,
                CustomFields = customFields
            };

            Logger.LogTrace("Resolving AWS Credentials object.");
            AwsExtensionCredential providedCredentials;
            try
            {
                providedCredentials = AuthUtilities.GetCredentials(authParams);
            }
            catch (Exception ex)
            {
                Logger.LogError($"An error occurred while trying to get AWS Credentials for {storeRef}.");
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = jobConfiguration.JobHistoryId,
                    FailureMessage = $"Failed to resolve AWS credentials for {storeRef}: {ex.Message}"
                };
            }
            string authMethod = AuthDescription.DescribeMethod(customFields, roleArn);
            Logger.LogInformation($"AWS credential method resolved: [{authMethod}] for {storeRef}.");
            Logger.LogTrace("AWS Credentials resolved.");

            // perform add or remove
            if (jobConfiguration.OperationType.ToString() == "Add")
            {
                Logger.LogTrace("Performing Management Add.");
                return PerformAddition(providedCredentials, jobConfiguration);
            }
            else if (jobConfiguration.OperationType.ToString() == "Remove")
            {
                Logger.LogTrace("Performing Management Remove.");
                return PerformRemoval(providedCredentials, jobConfiguration);
            }
            else
            {
                Logger.LogError($"Unrecognized Management Operation Type: {jobConfiguration.OperationType} for {storeRef}");
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = jobConfiguration.JobHistoryId,
                    FailureMessage = $"Invalid Management operation '{jobConfiguration.OperationType}' for {storeRef}."
                };
            }
        }

        internal JobResult PerformAddition(AwsExtensionCredential awsCredentials, ManagementJobConfiguration config)
        {
            string storeRef = Inventory.StoreRef(config.CertificateStoreDetails.ClientMachine,
                config.CertificateStoreDetails.StorePath);
            try
            {
                Logger.MethodEntry();

                List<Amazon.CertificateManager.Model.Tag> acmTags = ParseACMTags(config.JobProperties);

                Logger.LogDebug($"Certificate Add job will target AWS Region - {awsCredentials.Region.SystemName}");
                AcmClient = new AmazonCertificateManagerClient(awsCredentials.GetAwsCredentialObject(), awsCredentials.Region);
                Logger.LogTrace("ACM client created with loaded AWS Credentials and specified Region.");

                using (AcmClient)
                {
                    if (!string.IsNullOrWhiteSpace(config.JobCertificate.PrivateKeyPassword)) // This is a PFX Entry
                    {
                        Logger.LogTrace($"Found Private Key password.");
                        if (IsAcmCertificateArn(config.JobCertificate.Alias))
                        {
                            // Alias is an ACM certificate ARN, so this is a replace / renewal of an existing cert
                            Logger.LogDebug($"ACM ARN supplied as alias, validating existing cert can be renewed / replaced: {config.JobCertificate.Alias}");
                            // ARN Provided, Verify It is Not A PCA/Amazon Issued Cert
                            DescribeCertificateResponse DescribeCertificateResponse = AsyncHelpers.RunSync(() => AcmClient.DescribeCertificateAsync(config.JobCertificate.Alias));
                            Logger.LogTrace($"DescribeCertificateResponse JSON: {JsonConvert.SerializeObject(DescribeCertificateResponse)}");

                            if (DescribeCertificateResponse.Certificate.Type != CertificateType.IMPORTED)
                            {
                                Logger.LogError($"Non User Imported Certificate Type Found for {storeRef}, alias {config.JobCertificate.Alias}");
                                return new JobResult
                                {
                                    Result = OrchestratorJobStatusJobResult.Failure,
                                    JobHistoryId = config.JobHistoryId,
                                    FailureMessage =
                                        $"AWS Certificate Manager only supports overwriting user-imported certificates. The certificate " +
                                        $"'{config.JobCertificate.Alias}' in {storeRef} is Amazon-issued or Private CA-issued and cannot be replaced."
                                };
                            }
                        }

                        // Load PFX
                        Logger.LogTrace($"Loading certificate content: {config.JobCertificate.Contents}");
                        byte[] pfxBytes = Convert.FromBase64String(config.JobCertificate.Contents);
                        Pkcs12Store p;
                        using (var pfxBytesMemoryStream = new MemoryStream(pfxBytes))
                        {
                            p = new Pkcs12Store(pfxBytesMemoryStream, config.JobCertificate.PrivateKeyPassword.ToCharArray());
                        }
                        Logger.LogTrace($"Created Pkcs12Store");

                        // Extract private key
                        String alias;
                        String privateKeyString;
                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            using (TextWriter streamWriter = new StreamWriter(memoryStream))
                            {
                                PemWriter pemWriter = new PemWriter(streamWriter);

                                alias = (p.Aliases.Cast<string>()).SingleOrDefault(a => p.IsKeyEntry(a));
                                AsymmetricKeyParameter publicKey = p.GetCertificate(alias).Certificate.GetPublicKey();

                                AsymmetricKeyEntry KeyEntry = p.GetKey(alias);//Don't really need alias?
                                if (KeyEntry == null)
                                {
                                    throw new Exception("Unable to retrieve private key");
                                }

                                AsymmetricKeyParameter privateKey = KeyEntry.Key;
                                AsymmetricCipherKeyPair keyPair = new AsymmetricCipherKeyPair(publicKey, privateKey);

                                pemWriter.WriteObject(keyPair.Private);
                                streamWriter.Flush();
                                privateKeyString = Encoding.ASCII.GetString(memoryStream.GetBuffer()).Trim().Replace("\r", "").Replace("\0", "");
                                Logger.LogTrace("Loaded private key.");
                                memoryStream.Close();
                                streamWriter.Close();
                            }
                        }

                        string certPem = certStart + _pemify(Convert.ToBase64String(p.GetCertificate(alias).Certificate.GetEncoded())) + certEnd;
                        Logger.LogTrace($"Got certPem {certPem}");
                        //Create Memory Stream For Server Cert
                        ImportCertificateRequest icr;
                        ImportCertificateResponse IcrResponse;
                        using (MemoryStream serverCertStream = CertStringToStream(certPem))
                        {
                            using (MemoryStream privateStream = CertStringToStream(privateKeyString))
                            {
                                using (MemoryStream chainStream = GetChain(p, alias))
                                {
                                    icr = new ImportCertificateRequest
                                    {
                                        Certificate = serverCertStream,
                                        PrivateKey = privateStream,
                                        CertificateChain = chainStream
                                    };
                                
                                    icr.CertificateArn = IsAcmCertificateArn(config.JobCertificate.Alias) ? config.JobCertificate.Alias.Trim() : null; //If an ACM certificate ARN is provided, reimport in place (renewal/replace); otherwise import as a new certificate
                                    Logger.LogTrace($"Certificate arn {icr.CertificateArn}");
                                    
                                    if (icr.CertificateArn == null && acmTags != null && acmTags.Count > 0)
                                    {
                                        Logger.LogDebug($"Number of ACM tags added to certificate: {acmTags.Count}");
                                        icr.Tags = acmTags;
                                    }
                                    else
                                    {
                                        Logger.LogDebug("No ACM tags were added to the certificate");
                                    }
                        
                                    IcrResponse = AsyncHelpers.RunSync(() => AcmClient.ImportCertificateAsync(icr));
                                }
                            }
                        }
                        Logger.LogTrace($"IcrResponse JSON: {JsonConvert.SerializeObject(IcrResponse)}");
                        // Ensure 200 Response
                        if (IcrResponse.HttpStatusCode == HttpStatusCode.OK)
                        {
                            // FailureMessage is JobResult's only message field; Command shows it on success
                            // too, so populate it to surface the outcome (Success status keeps the job green).
                            string successMsg = $"Certificate {(icr.CertificateArn != null ? "renewed/replaced" : "imported")} into ACM " +
                                $"region {awsCredentials.Region.SystemName} for {storeRef}: ARN={IcrResponse.CertificateArn}.";
                            Logger.LogInformation(successMsg);
                            return new JobResult
                            {
                                Result = OrchestratorJobStatusJobResult.Success,
                                JobHistoryId = config.JobHistoryId,
                                FailureMessage = successMsg
                            };
                        }
                        else
                        {
                            Logger.LogError($"Certificate import into {storeRef} reported failure. HTTP status code: {IcrResponse.HttpStatusCode}");
                            return new JobResult
                            {
                                Result = OrchestratorJobStatusJobResult.Failure,
                                JobHistoryId = config.JobHistoryId,
                                FailureMessage =
                                    $"ACM ImportCertificate for {storeRef} returned HTTP {IcrResponse.HttpStatusCode} instead of OK."
                            };
                        }
                    }
                    else  // Non-PFX
                    {
                        Logger.LogError($"Certificate for {storeRef} did not have a private key password. Only PFX certificates may be added.");
                        return new JobResult
                        {
                            Result = OrchestratorJobStatusJobResult.Failure,
                            JobHistoryId = config.JobHistoryId,
                            FailureMessage =
                                $"Certificate must be a PFX (with a private key) to import into {storeRef}."
                        };
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Error performing certificate addition to {storeRef}: {e.Message}");
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage =
                        $"Error adding certificate to {storeRef}: {e.Message}"
                };
            }
        }

        internal JobResult PerformRemoval(AwsExtensionCredential awsCredentials, ManagementJobConfiguration config)
        {
            string storeRef = Inventory.StoreRef(config.CertificateStoreDetails.ClientMachine,
                config.CertificateStoreDetails.StorePath);
            try
            {
                Logger.MethodEntry();

                if (string.IsNullOrEmpty(config.JobCertificate.Alias))
                {
                    Logger.LogError($"A certificate Alias containing the ARN is required in order to remove a certificate from {storeRef}.");
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Failure,
                        JobHistoryId = config.JobHistoryId,
                        FailureMessage = $"A certificate Alias (the ACM ARN) is required to remove a certificate from {storeRef}, but none was provided."
                    };
                }

                Logger.LogTrace($"Certificate Alias - {config.JobCertificate.Alias}");

                // deprecated method of finding region, it is now specifically known in Certificate Store Path field
                //var endpoint = RegionEndpoint.GetBySystemName(config.JobCertificate.Alias.Split(":")[3]); //Get from ARN so user does not have to enter
                //Logger.LogTrace($"Got Endpoint From ARN from Certificate Alias: {JsonConvert.SerializeObject(endpoint)}");


                Logger.LogDebug($"Certificate Remove job will target AWS Region - {awsCredentials.Region.SystemName}");
                AcmClient = new AmazonCertificateManagerClient(awsCredentials.GetAwsCredentialObject(), awsCredentials.Region);
                Logger.LogTrace("ACM client created with loaded AWS Credentials and specified Region.");

                using (AcmClient)
                {
                    DeleteCertificateRequest deleteRequest = new DeleteCertificateRequest(config.JobCertificate.Alias);
                    Logger.LogTrace($"deleteRequest JSON: {JsonConvert.SerializeObject(deleteRequest)}");
                    DeleteCertificateResponse DeleteResponse = AsyncHelpers.RunSync(() => AcmClient.DeleteCertificateAsync(deleteRequest));
                    Logger.LogTrace($"DeleteResponse JSON: {JsonConvert.SerializeObject(DeleteResponse)}");
                    if (DeleteResponse.HttpStatusCode == HttpStatusCode.OK)
                    {
                        string successMsg = $"Certificate removed from ACM region {awsCredentials.Region.SystemName} for {storeRef}: ARN={config.JobCertificate.Alias}.";
                        Logger.LogInformation(successMsg);
                        return new JobResult
                        {
                            Result = OrchestratorJobStatusJobResult.Success,
                            JobHistoryId = config.JobHistoryId,
                            FailureMessage = successMsg
                        };
                    }
                    else
                    {
                        Logger.LogError($"Certificate removal from {storeRef} (ARN={config.JobCertificate.Alias}) reported failure. HTTP status code: {DeleteResponse.HttpStatusCode}");
                        return new JobResult
                        {
                            Result = OrchestratorJobStatusJobResult.Failure,
                            JobHistoryId = config.JobHistoryId,
                            FailureMessage =
                                $"ACM DeleteCertificate for {storeRef} (ARN={config.JobCertificate.Alias}) returned HTTP {DeleteResponse.HttpStatusCode} instead of OK."
                        };
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Error performing certificate removal from {storeRef}: {e.Message}");
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage =
                        $"Error removing certificate from {storeRef}: {e.Message}"
                };
            }
        }

        private static MemoryStream GetChain(Pkcs12Store store, string alias)
        {
            string ccs = "";

            X509CertificateEntry[] chain = store.GetCertificateChain(alias);

            // BouncyCastle returns the chain with the leaf/end-entity certificate as element [0],
            // followed by any intermediates (and root). The leaf is already sent separately as the
            // Certificate body of the ImportCertificateRequest, so it must NOT be repeated here;
            // ACM's CertificateChain is expected to contain only the intermediate (and root) certs.
            // Including the leaf caused it to appear twice within the published certificate's chain.
            if (chain == null || chain.Length <= 1)
            {
                // Only the leaf is present (no intermediates) - omit the chain entirely rather than
                // sending an empty value, which ACM may reject as an unparseable certificate chain.
                return null;
            }

            foreach (X509CertificateEntry chainEntry in chain.Skip(1))
            {
                ccs += certStart + _pemify(Convert.ToBase64String(chainEntry.Certificate.GetEncoded())) + certEnd + "\n";
            }

            return CertStringToStream(ccs);
        }

        //Convert String To MemoryStream
        private static MemoryStream CertStringToStream(string certString)
        {
            // Builds a MemoryStream from the Base64 Encoded String Representation of a cert
            byte[] certBytes = Encoding.ASCII.GetBytes(certString);
            return new MemoryStream(certBytes);
        }

        /// <summary>
        /// Determines whether the supplied alias is an AWS Certificate Manager certificate ARN
        /// (e.g. arn:aws:acm:&lt;region&gt;:&lt;account&gt;:certificate/&lt;id&gt;). When it is, the certificate
        /// already exists in ACM and an Add job should reimport in place (renewal/replace);
        /// otherwise the certificate is imported as a new one and ACM assigns a fresh ARN.
        /// Replaces an earlier alias-length heuristic that could misclassify a long friendly alias.
        /// </summary>
        internal static bool IsAcmCertificateArn(string alias)
        {
            if (string.IsNullOrWhiteSpace(alias)) return false;

            string trimmed = alias.Trim();
            return trimmed.StartsWith("arn:aws:acm:", StringComparison.OrdinalIgnoreCase)
                && trimmed.Contains(":certificate/");
        }

        private List<Amazon.CertificateManager.Model.Tag> ParseACMTags(Dictionary<string, object> jobProperties)
        {
            List<Amazon.CertificateManager.Model.Tag> acmTags = new List<Amazon.CertificateManager.Model.Tag>();

            if (jobProperties != null && jobProperties.ContainsKey("ACM Tags") && jobProperties["ACM Tags"] != null)
            {
                string acmTagsString = jobProperties["ACM Tags"].ToString();
                string[] acmTagsAry = acmTagsString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach(string acmTagString in acmTagsAry)
                {
                    string[] acmTagAry = acmTagString.Split("=", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (acmTagAry.Length != 2)
                    {
                        throw new Exception($"Error parsing ACM Tags - invalid format.  Found {acmTagAry.Length.ToString()} items for a tag instead of 2 (key/value).");
                    }

                    Amazon.CertificateManager.Model.Tag acmTag = new Amazon.CertificateManager.Model.Tag() { Key = acmTagAry[0], Value = acmTagAry[1] };
                    acmTags.Add(acmTag);
                }
            }

            return acmTags;
        }
    }
}
