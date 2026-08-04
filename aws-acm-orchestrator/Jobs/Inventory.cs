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

            Logger.LogTrace("Deserializing Store Properties to AuthCustomFieldParameters object.");
            AuthCustomFieldParameters customFields = JsonConvert.DeserializeObject<AuthCustomFieldParameters>(jobConfiguration.CertificateStoreDetails.Properties,
                    new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });
            Logger.LogTrace("Deserialized Store Properties.");

            // StorePath may be a legacy region ("us-east-1") with the Role ARN in ClientMachine, or
            // a self-contained "<roleArn>|<region>" emitted by cross-account Discovery. Parse handles both.
            var (roleArn, region) = StorePathParser.Parse(
                jobConfiguration.CertificateStoreDetails.StorePath,
                jobConfiguration.CertificateStoreDetails.ClientMachine);

            string storeRef = StoreRef(jobConfiguration.CertificateStoreDetails.ClientMachine,
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
            Logger.LogTrace("AWS Credentials resolved. Performing Inventory.");

            return PerformInventory(providedCredentials, jobConfiguration, submitInventoryUpdate);
        }

        internal JobResult PerformInventory(AwsExtensionCredential awsCredentials, InventoryJobConfiguration config, SubmitInventoryUpdate siu)
        {
            Logger.MethodEntry();
            bool warningFlag = false;
            int totalCertificates = 0;
            string storeRef = StoreRef(config.CertificateStoreDetails.ClientMachine,
                config.CertificateStoreDetails.StorePath);
            try
            {
                List<CurrentInventoryItem> inventoryItems = new List<CurrentInventoryItem>();

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

                int skipped = totalCertificates - inventoryItems.Count;
                string region = awsCredentials.Region.SystemName;
                Logger.LogTrace($"inventoryItems Response JSON: {JsonConvert.SerializeObject(inventoryItems)}");

                siu.Invoke(inventoryItems);

                if (warningFlag)
                {
                    string warnSummary = $"Inventory of ACM region {region} for {storeRef} completed with warnings: found {totalCertificates} certificate(s), " +
                        $"reported {inventoryItems.Count} to Keyfactor Command, and skipped {skipped} that could not be retrieved from ACM " +
                        "(the certificate ARN and error for each skipped item are logged individually above).";
                    Logger.LogWarning(warnSummary);
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Warning,
                        JobHistoryId = config.JobHistoryId,
                        FailureMessage = warnSummary
                    };
                }
                else
                {
                    // JobResult exposes only FailureMessage; Command renders it as the job's message
                    // regardless of status, so populating it on success surfaces the outcome in Command
                    // (the Success result is what marks the job green — the text is informational only).
                    string successSummary = $"Inventory of ACM region {region} for {storeRef} succeeded: found {totalCertificates} " +
                        $"certificate(s) in ACM and reported all {inventoryItems.Count} to Keyfactor Command.";
                    Logger.LogInformation(successSummary);
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Success,
                        JobHistoryId = config.JobHistoryId,
                        FailureMessage = successSummary
                    };
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Error ocurred in Perform Inventory for {storeRef}: {e.Message}");
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage = $"Error occurred during inventory of {storeRef}: {e.Message}"
                };
            }
        }

        // A human-readable identifier for the certificate store this job is acting on, so success,
        // warning, and failure messages surfaced in Keyfactor Command name the exact store. A store's
        // identity is its ClientMachine (the Role ARN, for legacy stores) plus its StorePath (the region,
        // or the self-contained "<roleArn>|<region>" emitted by cross-account Discovery).
        internal static string StoreRef(string clientMachine, string storePath)
        {
            return $"store [ClientMachine='{clientMachine}', StorePath='{storePath}']";
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
