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
using System;
using System.Collections.Generic;
using System.Linq;

using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Keyfactor.Extensions.Orchestrator.Aws.Acm.Jobs
{
    public class Inventory : IInventoryJobExtension
    {
        public string ExtensionName => "AWS-ACM-v3";

        internal IAmazonCertificateManager AcmClient;
        internal ILogger Logger;
        internal IPAMSecretResolver PamSecretResolver;

        internal AwsAuthUtility AuthUtilities;

        public Inventory(IPAMSecretResolver pam, ILogger<Inventory> logger)
        {
            PamSecretResolver = pam;
            Logger = logger;
            AuthUtilities = new AwsAuthUtility(pam);
        }

        public JobResult ProcessJob(InventoryJobConfiguration jobConfiguration, SubmitInventoryUpdate submitInventoryUpdate)
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

            Logger.LogTrace("AWS Credentials resolved. Performing Inventory.");
            return PerformInventory(providedCredentials, jobConfiguration, submitInventoryUpdate);
        }

        internal JobResult PerformInventory(AwsExtensionCredential awsCredentials, InventoryJobConfiguration config, SubmitInventoryUpdate siu)
        {
            Logger.MethodEntry();
            bool warningFlag = false;
            int totalCertificates = 0;
            try
            {
                List<CurrentInventoryItem> inventoryItems = new List<CurrentInventoryItem>();

                try
                {
                    Logger.LogDebug($"Certificate Inventory job will target AWS Region - {awsCredentials.Region.SystemName}");
                    AcmClient = new AmazonCertificateManagerClient(awsCredentials.GetAwsCredentialObject(), awsCredentials.Region);
                    Logger.LogTrace("ACM client created with loaded AWS Credentials and specified Region.");


                    var certList = AsyncHelpers.RunSync(() => AcmClient.ListCertificatesAsync());
                    Logger.LogDebug($"Found {certList.CertificateSummaryList.Count} Certificates");
                    Logger.LogTrace($"Cert List JSON: \n{JsonConvert.SerializeObject(certList)}");

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
                                    return BuildInventoryItem(c.CertificateArn);
                                }
                                catch
                                {
                                    Logger.LogWarning($"Could not fetch the certificate: {c?.DomainName} associated with arn {c?.CertificateArn}.");
                                    warningFlag = true;
                                    return new CurrentInventoryItem();
                                }
                            }).Where(acsii => acsii?.Certificates != null).ToList());

                        req.NextToken = AllCertificates.NextToken;
                    } while (AllCertificates.NextToken != null);

                    Logger.LogDebug($"Found {totalCertificates} Total Certificates In Amazon Certificate Manager Inventory Job.");
                    Logger.LogTrace($"inventoryItems Response JSON: {JsonConvert.SerializeObject(inventoryItems)}");
                }
                catch (Exception e)
                {
                    warningFlag = true;
                    Logger.LogError(e, "An error occurred while processing the Inventory.");
                }

                siu.Invoke(inventoryItems);

                if (warningFlag)
                {
                    Logger.LogWarning("Found Warning(s) during inventory.");
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Warning,
                        JobHistoryId = config.JobHistoryId,
                        FailureMessage = "Check the orchestrator logs for warnings or errors that ocurred during the inventory."
                    };
                }
                else
                {
                    Logger.LogTrace("No warnings found during Inventory. Reporting success.");
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
                Logger.LogError($"Error ocurred in Perform Inventory: {e.Message}");
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage = $"Error occurred in Perform Inventory: {e.Message}"
                };
            }
        }

        protected virtual CurrentInventoryItem BuildInventoryItem(string alias)
        {
            try
            {
                Logger.MethodEntry();
                string certificate = GetCertificateFromArn(alias);
                Logger.LogTrace($"Certificate: {certificate}");
                string base64Cert = RemoveAnchors(certificate);
                Logger.LogTrace($"Base64 Certificate: {base64Cert}");
                var entryParams = new Dictionary<string, object>
                {
                    { "ACM Tags", GetCertificateTagsFromArn(alias) }
                };
                CurrentInventoryItem acsi = new CurrentInventoryItem()
                {
                    Alias = alias,
                    Certificates = new[] { base64Cert },
                    ItemStatus = OrchestratorInventoryItemStatus.Unknown,
                    PrivateKeyEntry = true,
                    UseChainLevel = false,
                    Parameters = entryParams
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

        private string GetCertificateTagsFromArn(string arn)
        {
            try
            {
                Logger.MethodEntry();
                Logger.LogTrace($"arn: {arn}");
                ListTagsForCertificateRequest getTagsRequest = new ListTagsForCertificateRequest() { CertificateArn = arn };
                ListTagsForCertificateResponse getTagsResponse = AsyncHelpers.RunSync(() => AcmClient.ListTagsForCertificateAsync(getTagsRequest));

                string tags = "";
                foreach (Amazon.CertificateManager.Model.Tag tag in getTagsResponse.Tags)
                {
                    tags += $",{tag.Key}={tag.Value}";
                }

                return tags.Length > 0 ? tags.Substring(1) : tags; 
            }
            catch (Exception e)
            {
                Logger.LogError($"Error Occurred in Inventory.GetCertificateTagsFromArn: {e.Message}");
                throw;
            }
            finally
            { 
                Logger.MethodExit(); 
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
