﻿// Copyright 2024 Keyfactor
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
using Amazon;
using Org.BouncyCastle.OpenSsl;
using System.Linq;

using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Keyfactor.AnyAgent.AwsCertificateManager.Jobs
{
    abstract public class Management
    {
        private static String certStart = "-----BEGIN CERTIFICATE-----\n";
        private static String certEnd = "\n-----END CERTIFICATE-----";

        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        private static Func<String, String> _pemify = (ss => ss.Length <= 64 ? ss : ss.Substring(0, 64) + "\n" + _pemify(ss.Substring(64)));

        internal IAmazonCertificateManager AcmClient;
        internal ILogger Logger;

        internal JobResult PerformAddition(Credentials awsCredentials, ManagementJobConfiguration config)
        {
            try
            {
                Logger.MethodEntry();

                var endpoint = RegionEndpoint.GetBySystemName(config.JobProperties["AWS Region"].ToString());
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
                    AcmClient = new AmazonCertificateManagerClient(awsCredentials.AccessKeyId,
                        awsCredentials.SecretAccessKey, region: endpoint,
                        awsSessionToken: awsCredentials.SessionToken);
                }

                Logger.LogTrace($"AcmClient JSON: {JsonConvert.SerializeObject(AcmClient)}");

                using (AcmClient)
                {
                    if (!String.IsNullOrWhiteSpace(config.JobCertificate.PrivateKeyPassword)) // This is a PFX Entry
                    {
                        Logger.LogTrace($"Found Private Key {config.JobCertificate.PrivateKeyPassword}");
                        if (!String.IsNullOrWhiteSpace(config.JobCertificate.Alias))
                        {
                            Logger.LogTrace($"No Alias Found");
                            //ARN Provided, Verify It is Not A PCA/Amazon Issued Cert
                            DescribeCertificateResponse DescribeCertificateResponse = AsyncHelpers.RunSync(() => AcmClient.DescribeCertificateAsync(config.JobCertificate.Alias));
                            Logger.LogTrace($"DescribeCertificateResponse JSON: {JsonConvert.SerializeObject(DescribeCertificateResponse)}");

                            if (DescribeCertificateResponse.Certificate.Type != CertificateType.IMPORTED)
                            {
                                Logger.LogTrace($"Non User Imported Certificate Type Found");
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

                                alias = (p.Aliases.Cast<String>()).SingleOrDefault(a => p.IsKeyEntry(a));
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
                                Logger.LogTrace($"Got Private Key String {privateKeyString}");
                                memoryStream.Close();
                                streamWriter.Close();
                            }
                        }

                        String certPem = certStart + _pemify(Convert.ToBase64String(p.GetCertificate(alias).Certificate.GetEncoded())) + certEnd;
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
                        Logger.LogTrace($"Certificate arn {icr.CertificateArn}");

                        ImportCertificateResponse IcrResponse = AsyncHelpers.RunSync(() => AcmClient.ImportCertificateAsync(icr));
                        Logger.LogTrace($"IcrResponse JSON: {JsonConvert.SerializeObject(IcrResponse)}");
                        // Ensure 200 Response
                        if (IcrResponse.HttpStatusCode == HttpStatusCode.OK)
                        {
                            Logger.LogTrace($"Return Success");
                            return new JobResult
                            {
                                Result = OrchestratorJobStatusJobResult.Success,
                                JobHistoryId = config.JobHistoryId,
                                FailureMessage = ""
                            };
                        }
                        else
                        {
                            Logger.LogTrace($"Return Failure");
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
                        Logger.LogTrace($"Return PFX Failure");
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
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage =
                        $"Management/Add {e.Message}"
                };
            }
        }

        internal JobResult PerformRemoval(Credentials awsCredentials, ManagementJobConfiguration config)
        {
            try
            {
                Logger.MethodEntry();

                var endpoint = RegionEndpoint.GetBySystemName(config.JobCertificate.Alias.Split(":")[3]); //Get from ARN so user does not have to enter
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
                    AcmClient = new AmazonCertificateManagerClient(awsCredentials.AccessKeyId,
                        awsCredentials.SecretAccessKey, region: endpoint,
                        awsSessionToken: awsCredentials.SessionToken);
                }

                Logger.LogTrace($"AcmClient JSON: {JsonConvert.SerializeObject(AcmClient)}");

                using (AcmClient)
                {
                    DeleteCertificateRequest deleteRequest = new DeleteCertificateRequest(config.JobCertificate.Alias);
                    Logger.LogTrace($"deleteRequest JSON: {JsonConvert.SerializeObject(deleteRequest)}");
                    DeleteCertificateResponse DeleteResponse = AsyncHelpers.RunSync(() => AcmClient.DeleteCertificateAsync(deleteRequest));
                    Logger.LogTrace($"DeleteResponse JSON: {JsonConvert.SerializeObject(DeleteResponse)}");
                    if (DeleteResponse.HttpStatusCode == HttpStatusCode.OK)
                    {
                        Logger.LogTrace($"Return Success");
                        return new JobResult
                        {
                            Result = OrchestratorJobStatusJobResult.Success,
                            JobHistoryId = config.JobHistoryId,
                            FailureMessage = ""
                        };
                    }
                    else
                    {
                        Logger.LogTrace($"Return Failure");
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
    }
}
