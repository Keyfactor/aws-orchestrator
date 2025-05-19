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

using Amazon.CertificateManager.Model;
using Amazon.CertificateManager;
using Amazon.Runtime.Internal.Util;
using Amazon.SecurityToken.Model;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Pkcs;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Generic;
using Amazon;
using Org.BouncyCastle.OpenSsl;
using System.Linq;

using ILogger = Microsoft.Extensions.Logging.ILogger;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using static Org.BouncyCastle.Math.EC.ECCurve;
using System.Drawing;
using Amazon.IdentityManagement.Model;
using aws_auth_library;

namespace Keyfactor.AnyAgent.AwsCertificateManager.Jobs
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
            AuthUtilities = new AwsAuthUtility(pam, logger);
        }

        public JobResult ProcessJob(ManagementJobConfiguration jobConfiguration)
        {
            Logger.MethodEntry();
            Logger.LogTrace($"Deserializing Cert Store Properties: {jobConfiguration.CertificateStoreDetails.Properties}");
            AuthCustomFieldParameters customFields = JsonConvert.DeserializeObject<AuthCustomFieldParameters>(jobConfiguration.CertificateStoreDetails.Properties,
                    new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });
            //
            // TODO: Prevent logging of credentials, changes to custom fields in this release means logging this object (AND Properties above) logs credentials!!
            //
            Logger.LogTrace($"Populated ACMCustomFields: {JsonConvert.SerializeObject(customFields)}");

            AuthenticationParameters authParams = new AuthenticationParameters
            {
                RoleARN = jobConfiguration.CertificateStoreDetails.ClientMachine,
                Region = jobConfiguration.CertificateStoreDetails.StorePath,
                CustomFields = customFields
            };

            Logger.LogTrace("Resolving AWS Credentials object.");
            AwsExtensionCredential providedCredentials = AuthUtilities.GetCredentials(authParams);
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
                Logger.LogError($"Unrecognized Management Operation Type: {jobConfiguration.OperationType}");
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = jobConfiguration.JobHistoryId,
                    FailureMessage = "Invalid Management Operation"
                };
            }
        }

        internal JobResult PerformAddition(AwsExtensionCredential awsCredentials, ManagementJobConfiguration config)
        {
            try
            {
                Logger.MethodEntry();

                string region;
                if (config.JobProperties.ContainsKey("AWS Region"))
                {
                    region = config.JobProperties["AWS Region"].ToString();
                }
                else
                {
                    var errorMessage = "Required field for Management Job - AWS Region - was not present.";
                    Logger.LogError(errorMessage);
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Failure,
                        JobHistoryId = config.JobHistoryId,
                        FailureMessage = errorMessage
                    };
                }

                List<Amazon.CertificateManager.Model.Tag> acmTags = ParseACMTags(config.JobProperties);

                Logger.LogTrace($"Targeting AWS Region - {region}");
                var endpoint = RegionEndpoint.GetBySystemName(region);
                Logger.LogTrace($"Got Endpoint From Job Properties JSON: {JsonConvert.SerializeObject(endpoint)}");

                if (awsCredentials == null)
                {
                    // use default SDK auth for ACM client
                    Logger.LogDebug("Using default credential lookup methods through the AWS SDK");
                    AcmClient = new AmazonCertificateManagerClient(region: endpoint);
                }
                else
                {
                    // use credentials configured by assuming a role through AWS STS
                    Logger.LogDebug("Using credentials from assuming a Role through AWS STS");
                    AcmClient = new AmazonCertificateManagerClient(awsCredentials.GetAwsCredentialObject(), awsCredentials.Region);
                }

                using (AcmClient)
                {
                    if (!string.IsNullOrWhiteSpace(config.JobCertificate.PrivateKeyPassword)) // This is a PFX Entry
                    {
                        Logger.LogTrace($"Found Private Key password.");
                        if (!string.IsNullOrWhiteSpace(config.JobCertificate.Alias))
                        {
                            // Alias is specified, this is a replace / renewal
                            Logger.LogDebug($"Alias specified, validating existing cert can be renewed / replaced: {config.JobCertificate.Alias}");
                            // ARN Provided, Verify It is Not A PCA/Amazon Issued Cert
                            DescribeCertificateResponse DescribeCertificateResponse = AsyncHelpers.RunSync(() => AcmClient.DescribeCertificateAsync(config.JobCertificate.Alias));
                            Logger.LogTrace($"DescribeCertificateResponse JSON: {JsonConvert.SerializeObject(DescribeCertificateResponse)}");

                            if (DescribeCertificateResponse.Certificate.Type != CertificateType.IMPORTED)
                            {
                                Logger.LogError($"Non User Imported Certificate Type Found");
                                return new JobResult
                                {
                                    Result = OrchestratorJobStatusJobResult.Failure,
                                    JobHistoryId = config.JobHistoryId,
                                    FailureMessage =
                                        "Amazon Web Services Certificate Manager only supports overwriting user-imported certificates.\"), \"Management/Add"
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
                                }
                            }
                        }
                        icr.CertificateArn = config.JobCertificate.Alias?.Length >= 20 ? config.JobCertificate.Alias.Trim() : null; //If an arn is provided, use it, this will perform a renewal/replace
                        if (icr.CertificateArn == null )
                        {
                            icr.Tags = acmTags;
                        }
                        Logger.LogTrace($"Certificate arn {icr.CertificateArn}");
                        
                        ImportCertificateResponse IcrResponse = AsyncHelpers.RunSync(() => AcmClient.ImportCertificateAsync(icr));
                        Logger.LogTrace($"IcrResponse JSON: {JsonConvert.SerializeObject(IcrResponse)}");
                        // Ensure 200 Response
                        if (IcrResponse.HttpStatusCode == HttpStatusCode.OK)
                        {
                            Logger.LogTrace($"Certificate Import reported success.");
                            return new JobResult
                            {
                                Result = OrchestratorJobStatusJobResult.Success,
                                JobHistoryId = config.JobHistoryId,
                                FailureMessage = ""
                            };
                        }
                        else
                        {
                            Logger.LogError($"Certificate Import reported failure.");
                            Logger.LogError($"Failure HTTP status code: {IcrResponse.HttpStatusCode}");
                            return new JobResult
                            {
                                Result = OrchestratorJobStatusJobResult.Failure,
                                JobHistoryId = config.JobHistoryId,
                                FailureMessage =
                                    "Management/Add"
                            };
                        }
                    }
                    else  // Non-PFX
                    {
                        Logger.LogError($"Certificate did not have private key password. Only PFX certificates may be added.");
                        return new JobResult
                        {
                            Result = OrchestratorJobStatusJobResult.Failure,
                            JobHistoryId = config.JobHistoryId,
                            FailureMessage =
                                "Certificate Must be a PFX"
                        };
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Error in Performing Addition: {e.Message}");
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage =
                        $"Management/Add {e.Message}"
                };
            }
        }

        internal JobResult PerformRemoval(AwsExtensionCredential awsCredentials, ManagementJobConfiguration config)
        {
            try
            {
                Logger.MethodEntry();

                if (string.IsNullOrEmpty(config.JobCertificate.Alias))
                {
                    Logger.LogError("A certificate Alias containing the ARN is required in order to remove a certificate.");
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Failure,
                        JobHistoryId = config.JobHistoryId,
                        FailureMessage = "Alias is required but not present."
                    };
                }

                Logger.LogTrace($"Certificate Alias - {config.JobCertificate.Alias}");
                var endpoint = RegionEndpoint.GetBySystemName(config.JobCertificate.Alias.Split(":")[3]); //Get from ARN so user does not have to enter
                Logger.LogTrace($"Got Endpoint From ARN from Certificate Alias: {JsonConvert.SerializeObject(endpoint)}");

                if (awsCredentials == null)
                {
                    // use default SDK auth for ACM client
                    Logger.LogDebug("Using default credential lookup methods through the AWS SDK");
                    AcmClient = new AmazonCertificateManagerClient(region: endpoint);
                }
                else
                {
                    // use credentials configured by assuming a role through AWS STS
                    Logger.LogDebug("Using credentials from assuming a Role through AWS STS");
                    AcmClient = new AmazonCertificateManagerClient(awsCredentials.GetAwsCredentialObject(), awsCredentials.Region);
                }

                using (AcmClient)
                {
                    DeleteCertificateRequest deleteRequest = new DeleteCertificateRequest(config.JobCertificate.Alias);
                    Logger.LogTrace($"deleteRequest JSON: {JsonConvert.SerializeObject(deleteRequest)}");
                    DeleteCertificateResponse DeleteResponse = AsyncHelpers.RunSync(() => AcmClient.DeleteCertificateAsync(deleteRequest));
                    Logger.LogTrace($"DeleteResponse JSON: {JsonConvert.SerializeObject(DeleteResponse)}");
                    if (DeleteResponse.HttpStatusCode == HttpStatusCode.OK)
                    {
                        Logger.LogTrace($"Certificate Removal reported success.");
                        return new JobResult
                        {
                            Result = OrchestratorJobStatusJobResult.Success,
                            JobHistoryId = config.JobHistoryId,
                            FailureMessage = ""
                        };
                    }
                    else
                    {
                        Logger.LogError($"Certificate Removal reported failure.");
                        Logger.LogError($"Failure HTTP status code - {DeleteResponse.HttpStatusCode}");
                        return new JobResult
                        {
                            Result = OrchestratorJobStatusJobResult.Failure,
                            JobHistoryId = config.JobHistoryId,
                            FailureMessage =
                                "Management/Remove"
                        };
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Error in Perform Removal: {e.Message}");
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage =
                        $"Management/Remove: {e.Message}"
                };
            }
        }

        private static MemoryStream GetChain(Pkcs12Store store, string alias)
        {
            string ccs = "";

            X509CertificateEntry[] chain = store.GetCertificateChain(alias);

            foreach (X509CertificateEntry chainEntry in chain)
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
