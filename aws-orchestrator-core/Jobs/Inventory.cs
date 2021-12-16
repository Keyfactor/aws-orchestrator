using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Amazon;
using Amazon.CertificateManager;
using Amazon.CertificateManager.Model;
using Amazon.Runtime.Internal.Util;
using Keyfactor.AnyAgent.AwsCertificateManager.Models;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;
// ReSharper disable UnusedMember.Global


namespace Keyfactor.AnyAgent.AwsCertificateManager.Jobs
{

    public class Inventory : IInventoryJobExtension
    {

        private readonly ILogger<Inventory> _logger;

        public Inventory(ILogger<Inventory> logger) =>
            _logger = logger;

        protected internal virtual IAmazonCertificateManager AcmClient { get; set; }
        protected internal virtual ListCertificatesResponse AllCertificates { get; set; }
        protected internal virtual GetCertificateRequest GetCertificateRequest { get; set; }
        protected internal virtual GetCertificateResponse GetCertificateResponse { get; set; }
        protected internal virtual CustomFields CustomFields { get; set; }

        private AuthResponse OktaAuthenticate(InventoryJobConfiguration config)
        {
            try
            {
                _logger.MethodEntry();
                CustomFields = JsonConvert.DeserializeObject<CustomFields>(config.CertificateStoreDetails.Properties,
                    new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });
                var oktaAuthUrl =$"https://{config.CertificateStoreDetails.ClientMachine}/oauth2/default/v1/token";
                _logger.LogTrace($"Custom Field List: {CustomFields}");
                _logger.LogTrace($"Okta Auth URL: {oktaAuthUrl}");

                var client =
                    new RestClient(oktaAuthUrl)
                    {
                        Timeout = -1
                    };
                var request = new RestRequest(Method.POST);
                request.AddHeader("Accept", "application/json");
                var clientId = config.ServerUsername;
                var clientSecret = config.ServerPassword;
                var plainTextBytes = Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}");
                _logger.LogTrace($"Okta Auth Credentials: {plainTextBytes}");
                var authHeader = Convert.ToBase64String(plainTextBytes);
                request.AddHeader("Authorization", $"Basic {authHeader}");
                request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                if (CustomFields != null)
                {
                    request.AddParameter("grant_type", CustomFields.GrantType);
                    request.AddParameter("scope", CustomFields.Scope);
                }
                var response = client.Execute(request);
                _logger.LogTrace($"Okta Auth Raw Response: {response}");
                var authResponse = JsonConvert.DeserializeObject<AuthResponse>(response.Content);
                _logger.LogTrace($"Okta Serialized Auth Response: {JsonConvert.SerializeObject(authResponse)}");

                return authResponse;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occurred in Inventory.OktaAuthenticate: {e.Message}");
                throw;
            }
        }

        private string GetCertificateFromArn(string arn)
        {
            try
            {
                _logger.MethodEntry();
                _logger.LogTrace($"arn: {arn}");
                GetCertificateRequest = new GetCertificateRequest(arn);
                _logger.LogTrace($"GetCertificateRequest Serialized Auth Request: {JsonConvert.SerializeObject(GetCertificateRequest)}");
                GetCertificateResponse = AsyncHelpers.RunSync(() => AcmClient.GetCertificateAsync(GetCertificateRequest));
                _logger.LogTrace($"GetCertificateRequest Serialized Auth Response: {JsonConvert.SerializeObject(GetCertificateResponse)}");
                return GetCertificateResponse.Certificate;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occurred in Inventory.GetCertificateFromArn: {e.Message}");
                throw;
            }
        }

        //Remove Anchor Tags From Encoded Cert
        private string RemoveAnchors(string base64Cert)
        {
            try
            {
                _logger.MethodEntry();
                var noAnchors= base64Cert.Replace("\r", "")
                    .Replace("-----BEGIN CERTIFICATE-----\n", "")
                    .Replace("\n-----END CERTIFICATE-----\n", "");
                _logger.LogTrace($"No Anchors: {noAnchors}");
                return noAnchors;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occurred in Inventory.RemoveAnchors: {e.Message}");
                throw;
            }
        }

        protected virtual CurrentInventoryItem BuildInventoryItem(string alias, string region)
        {
            try
            {
                _logger.MethodEntry();
                string certificate = GetCertificateFromArn(alias);
                _logger.LogTrace($"Certificate: {certificate}");
                string base64Cert = RemoveAnchors(certificate);
                _logger.LogTrace($"Base64 Certificate: {base64Cert}");
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
                _logger.LogError($"Error Occurred in Inventory.BuildInventoryItem: {e.Message}");
                throw;
            }
        }

        private JobResult PerformInventory(InventoryJobConfiguration config, SubmitInventoryUpdate siu)
        {
            _logger.MethodEntry();
            bool warningFlag = false;
            int totalCertificates = 0;
            StringBuilder sb = new StringBuilder();
            sb.Append("");
            try
            {
                //Get List of Reasons from Cert Store Regions Param
                var authResponse = OktaAuthenticate(config);
                _logger.LogTrace($"Got authResponse: {JsonConvert.SerializeObject(authResponse)}");
                var regions = CustomFields.AwsRegions.Split(',');
                _logger.LogTrace($"Raw Regions CSV from AWSRegions Custom Field: {regions}");
                
                List<CurrentInventoryItem> inventoryItems = new List<CurrentInventoryItem>();

                foreach (var region in regions)
                {
                    try
                    {
                        var endPoint = RegionEndpoint.GetBySystemName(region);
                        _logger.LogTrace($"Mapped AWS Endpoint: {JsonConvert.SerializeObject(endPoint)}");
                        var credentials = Utilities.AwsAuthenticate(authResponse, endPoint, config.CertificateStoreDetails.StorePath, CustomFields.AwsRole);
                        _logger.LogTrace($"Credentials JSON: {JsonConvert.SerializeObject(credentials)}");
                        AcmClient = new AmazonCertificateManagerClient(credentials.AccessKeyId,
                            credentials.SecretAccessKey, region: RegionEndpoint.GetBySystemName(region),
                            awsSessionToken: credentials.SessionToken);
                        _logger.LogTrace($"AcmClient JSON: {JsonConvert.SerializeObject(AcmClient)}");
                        var certList = AsyncHelpers.RunSync(() => AcmClient.ListCertificatesAsync());
                        _logger.LogTrace($"First Cert List JSON For Region {region}: {JsonConvert.SerializeObject(certList)}");
                        Console.Write($"Found {certList.CertificateSummaryList.Count} Certificates\n");

                        ListCertificatesRequest req = new ListCertificatesRequest();

                        //The Current Workaround For AWS Not Returning Certs Without A SAN
                        List<String> keyTypes = new List<String> { KeyAlgorithm.RSA_1024, KeyAlgorithm.RSA_2048, KeyAlgorithm.RSA_4096, KeyAlgorithm.EC_prime256v1, KeyAlgorithm.EC_secp384r1, KeyAlgorithm.EC_secp521r1 };
                        req.Includes = new Filters() { KeyTypes = keyTypes };

                        //Only fetch certificates that have been issued at one point
                        req.CertificateStatuses = new List<string> { CertificateStatus.ISSUED, CertificateStatus.INACTIVE, CertificateStatus.EXPIRED, CertificateStatus.REVOKED };
                        req.MaxItems = 100;

                        _logger.LogTrace($"ListCertificatesRequest JSON: {JsonConvert.SerializeObject(req)}");

                        do
                        {
                            AllCertificates = AsyncHelpers.RunSync(() => AcmClient.ListCertificatesAsync(req));//Fetch batch of certificates from ACM API
                            _logger.LogTrace($"AllCertificates JSON: {JsonConvert.SerializeObject(AllCertificates)}");

                            totalCertificates += AllCertificates.CertificateSummaryList.Count;
                            _logger.LogDebug($"Found {AllCertificates.CertificateSummaryList.Count} Certificates In Batch Amazon Certificate Manager Job.");

                            inventoryItems.AddRange(AllCertificates.CertificateSummaryList.Select(
                                c =>
                                {
                                    try
                                    {
                                        return BuildInventoryItem(c.CertificateArn, region);
                                    }
                                    catch
                                    {
                                        _logger.LogWarning($"Could not fetch the certificate: {c?.DomainName} associated with arn {c?.CertificateArn}.");
                                        sb.Append($"Could not fetch the certificate: {c?.DomainName} associated with arn {c?.CertificateArn}.{Environment.NewLine}");
                                        warningFlag = true;
                                        return new CurrentInventoryItem();
                                    }
                                }).Where(acsii => acsii?.Certificates != null).ToList());

                            req.NextToken = AllCertificates.NextToken;

                        } while (AllCertificates.NextToken != null);

                        _logger.LogDebug($"Found {totalCertificates} Total Certificates In Amazon Certificate Manager Job.");
                        _logger.LogTrace($"inventoryItems Response JSON: {JsonConvert.SerializeObject(inventoryItems)}");
                    }
                    catch (Exception e) //have to loop through all regions specified for each account and some may be invalid
                    {
                        _logger.LogError($"Could not authenticate to AWS, invalid account/region combination account: {config.CertificateStoreDetails.StorePath} region: {region} error: {e.Message}");
                    }
                }

                siu.Invoke(inventoryItems);

                if (warningFlag)
                {
                    _logger.LogTrace("Found Warning");
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Warning,
                        JobHistoryId = config.JobHistoryId,
                        FailureMessage = ""
                    };
                }
                else
                {
                    _logger.LogTrace("Return Success");
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
                _logger.LogError(e.Message);
                throw;
            }
        }

        public string ExtensionName => "AWSCerManO";
        public JobResult ProcessJob(InventoryJobConfiguration jobConfiguration, SubmitInventoryUpdate submitInventoryUpdate)
        {
            _logger.MethodEntry();
            return PerformInventory(jobConfiguration, submitInventoryUpdate);
        }
    }
}
