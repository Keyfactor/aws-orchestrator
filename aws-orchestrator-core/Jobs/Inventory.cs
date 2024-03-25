// Copyright 2024 Keyfactor
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
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using Amazon;
using System.Linq;

using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Keyfactor.AnyAgent.AwsCertificateManager.Jobs
{
    abstract public class Inventory
    {
        internal IAmazonCertificateManager AcmClient;
        internal ILogger Logger;
        internal IPAMSecretResolver PamSecretResolver;

        internal AuthUtilities AuthUtilities;

        internal JobResult PerformInventory(Credentials awsCredentials, InventoryJobConfiguration config, SubmitInventoryUpdate siu)
        {
            Logger.MethodEntry();
            bool warningFlag = false;
            int totalCertificates = 0;
            StringBuilder sb = new StringBuilder();
            sb.Append("");
            try
            {
                //Get List of regions from Cert Store Path
                var regions = config.CertificateStoreDetails.StorePath.Split(','); // TODO: extract to named parameter in main inventory method
                Logger.LogTrace($"Raw Regions CSV from AWSRegions Custom Field: {regions}");

                List<CurrentInventoryItem> inventoryItems = new List<CurrentInventoryItem>();

                foreach (var region in regions)
                {
                    try
                    {
                        var endpoint = RegionEndpoint.GetBySystemName(region);
                        Logger.LogTrace($"Mapped AWS Endpoint: {JsonConvert.SerializeObject(endpoint)}");

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
                        var certList = AsyncHelpers.RunSync(() => AcmClient.ListCertificatesAsync());
                        Logger.LogTrace($"First Cert List JSON For Region {region}: {JsonConvert.SerializeObject(certList)}");
                        Console.Write($"Found {certList.CertificateSummaryList.Count} Certificates\n");

                        ListCertificatesRequest req = new ListCertificatesRequest();

                        //The Current Workaround For AWS Not Returning Certs Without A SAN
                        List<String> keyTypes = new List<String> { KeyAlgorithm.RSA_1024, KeyAlgorithm.RSA_2048, KeyAlgorithm.RSA_4096, KeyAlgorithm.EC_prime256v1, KeyAlgorithm.EC_secp384r1, KeyAlgorithm.EC_secp521r1 };
                        req.Includes = new Filters() { KeyTypes = keyTypes };

                        //Only fetch certificates that have been issued at one point
                        req.CertificateStatuses = new List<string> { CertificateStatus.ISSUED, CertificateStatus.INACTIVE, CertificateStatus.EXPIRED, CertificateStatus.REVOKED };
                        req.MaxItems = 100;

                        Logger.LogTrace($"ListCertificatesRequest JSON: {JsonConvert.SerializeObject(req)}");

                        ListCertificatesResponse AllCertificates;
                        do
                        {
                            AllCertificates = AsyncHelpers.RunSync(() => AcmClient.ListCertificatesAsync(req));//Fetch batch of certificates from ACM API
                            Logger.LogTrace($"AllCertificates JSON: {JsonConvert.SerializeObject(AllCertificates)}");

                            totalCertificates += AllCertificates.CertificateSummaryList.Count;
                            Logger.LogDebug($"Found {AllCertificates.CertificateSummaryList.Count} Certificates In Batch Amazon Certificate Manager Job.");

                            inventoryItems.AddRange(AllCertificates.CertificateSummaryList.Select(
                                c =>
                                {
                                    try
                                    {
                                        return BuildInventoryItem(c.CertificateArn, region);
                                    }
                                    catch
                                    {
                                        Logger.LogWarning($"Could not fetch the certificate: {c?.DomainName} associated with arn {c?.CertificateArn}.");
                                        sb.Append($"Could not fetch the certificate: {c?.DomainName} associated with arn {c?.CertificateArn}.{Environment.NewLine}");
                                        warningFlag = true;
                                        return new CurrentInventoryItem();
                                    }
                                }).Where(acsii => acsii?.Certificates != null).ToList());

                            req.NextToken = AllCertificates.NextToken;
                        } while (AllCertificates.NextToken != null);

                        Logger.LogDebug($"Found {totalCertificates} Total Certificates In Amazon Certificate Manager Job.");
                        Logger.LogTrace($"inventoryItems Response JSON: {JsonConvert.SerializeObject(inventoryItems)}");
                    }
                    catch (Exception e) //have to loop through all regions specified for each account and some may be invalid
                    {
                        // TODO: failed inventory is returning Success even when it cannot authenticate
                        Logger.LogError($"Could not authenticate to AWS, invalid account/region combination account: {config.CertificateStoreDetails.StorePath} region: {region} error: {e.Message}");
                    }
                }

                siu.Invoke(inventoryItems);

                if (warningFlag)
                {
                    Logger.LogTrace("Found Warning");
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Warning,
                        JobHistoryId = config.JobHistoryId,
                        FailureMessage = ""
                    };
                }
                else
                {
                    Logger.LogTrace("Return Success");
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Success,
                        JobHistoryId = config.JobHistoryId,
                        FailureMessage = ""
                    };
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e.Message);
                throw;
            }
        }

        protected virtual CurrentInventoryItem BuildInventoryItem(string alias, string region)
        {
            try
            {
                Logger.MethodEntry();
                string certificate = GetCertificateFromArn(alias);
                Logger.LogTrace($"Certificate: {certificate}");
                string base64Cert = RemoveAnchors(certificate);
                Logger.LogTrace($"Base64 Certificate: {base64Cert}");
                var regionDict = new Dictionary<string, object>
                {
                    { "AWS Region", region }
                };
                CurrentInventoryItem acsi = new CurrentInventoryItem()
                {
                    Alias = alias,
                    Certificates = new[] { base64Cert },
                    ItemStatus = OrchestratorInventoryItemStatus.Unknown,
                    PrivateKeyEntry = true,
                    UseChainLevel = false,
                    Parameters = regionDict
                };

                return acsi;
            }
            catch (Exception e)
            {
                Logger.LogError($"Error Occurred in Inventory.BuildInventoryItem: {e.Message}");
                throw;
            }
        }

        private string GetCertificateFromArn(string arn)
        {
            try
            {
                Logger.MethodEntry();
                Logger.LogTrace($"arn: {arn}");
                GetCertificateRequest GetCertificateRequest = new GetCertificateRequest(arn);
                Logger.LogTrace($"GetCertificateRequest Serialized Auth Request: {JsonConvert.SerializeObject(GetCertificateRequest)}");
                GetCertificateResponse GetCertificateResponse = AsyncHelpers.RunSync(() => AcmClient.GetCertificateAsync(GetCertificateRequest));
                Logger.LogTrace($"GetCertificateRequest Serialized Auth Response: {JsonConvert.SerializeObject(GetCertificateResponse)}");
                return GetCertificateResponse.Certificate;
            }
            catch (Exception e)
            {
                Logger.LogError($"Error Occurred in Inventory.GetCertificateFromArn: {e.Message}");
                throw;
            }
        }

        //Remove Anchor Tags From Encoded Cert
        private string RemoveAnchors(string base64Cert)
        {
            try
            {
                Logger.MethodEntry();
                var noAnchors = base64Cert.Replace("\r", "")
                    .Replace("-----BEGIN CERTIFICATE-----\n", "")
                    .Replace("\n-----END CERTIFICATE-----\n", "");
                Logger.LogTrace($"No Anchors: {noAnchors}");
                return noAnchors;
            }
            catch (Exception e)
            {
                Logger.LogError($"Error Occurred in Inventory.RemoveAnchors: {e.Message}");
                throw;
            }
        }
    }
}
