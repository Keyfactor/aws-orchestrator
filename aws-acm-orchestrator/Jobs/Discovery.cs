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

using Amazon;
using Amazon.CertificateManager;
using Amazon.CertificateManager.Model;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.Organizations;
using Amazon.Organizations.Model;
using Amazon.Runtime;
using Amazon.Runtime.Internal.Util;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
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
    public class Discovery : IDiscoveryJobExtension
    {
        // The default IAM role name expected to exist in every member account. Operators can override
        // it (see DiscoveryRoleName resolution below). Matches the integration manifest default.
        internal const string DefaultDiscoveryRoleName = "KeyfactorACMDiscoveryRole";

        public string ExtensionName => "AWS-ACM-v3";
        internal ILogger Logger;
        internal IPAMSecretResolver PamSecretResolver;
        internal AwsAuthUtility AuthUtilities;

        public Discovery(IPAMSecretResolver pam, ILogger<Discovery> logger)
        {
            PamSecretResolver = pam;
            Logger = logger;
            AuthUtilities = new AwsAuthUtility(pam);
        }

        public JobResult ProcessJob(DiscoveryJobConfiguration jobConfiguration, SubmitDiscoveryUpdate submitDiscovery)
        {
            Logger.MethodEntry();

            // JobProperties is Dictionary<string, object> on DiscoveryJobConfiguration; round-trip through JSON
            // to produce the AuthCustomFieldParameters that the auth library expects.
            Logger.LogTrace("Deserializing Job Properties to AuthCustomFieldParameters object.");
            string jobPropsJson = JsonConvert.SerializeObject(jobConfiguration.JobProperties);
            AuthCustomFieldParameters customFields = JsonConvert.DeserializeObject<AuthCustomFieldParameters>(
                jobPropsJson,
                new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });

            // The standard Keyfactor Discovery dialog exposes no auth-method fields (those live on the
            // store type, which Discovery doesn't have yet), so an operator-launched Discovery arrives
            // with all auth booleans false and AwsAuthUtility rejects it as "No Auth method selected".
            // Default to Default SDK auth using the host's ambient credentials (EC2 instance profile,
            // ~/.aws/credentials, AWS_* env). Whether to STS-assume into another role is decided below
            // once we know if "Directories to search" supplied a target ARN.
            bool defaultedAuth = false;
            if (!customFields.UseDefaultSdkAuth && !customFields.UseOAuth && !customFields.UseIAM)
            {
                Logger.LogInformation("No auth method selected in Discovery JobProperties; defaulting to Default SDK auth.");
                customFields.UseDefaultSdkAuth = true;
                defaultedAuth = true;
            }
            Logger.LogTrace("Deserialized Job Properties.");

            // DiscoveryJobConfiguration has no StorePath. Use a "Region" key from JobProperties as the
            // base region for credential resolution, or fall back to us-east-1.
            string baseRegion = GetJobProperty(jobConfiguration.JobProperties, "Region") ?? "us-east-1";

            // The management-account Role ARN to assume comes from the Discovery dialog's "Directories to
            // search" field. Standard Discovery dialog fields are packed into JobProperties at submission
            // time with a key name that varies across Keyfactor Command versions (dirs / Dirs /
            // DirectoriesToSearch), so search case-insensitively across the known variants. Falls back to
            // ClientMachine for older callers that wired the ARN there directly.
            string dirsValue = jobConfiguration.JobProperties?
                .Where(kv => kv.Key.Equals("dirs", StringComparison.OrdinalIgnoreCase)
                          || kv.Key.Equals("DirectoriesToSearch", StringComparison.OrdinalIgnoreCase))
                .Select(kv => kv.Value?.ToString())
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

            string roleArn = dirsValue?
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .FirstOrDefault(s => s.Length > 0);

            if (string.IsNullOrWhiteSpace(roleArn))
            {
                roleArn = jobConfiguration.ClientMachine;
            }

            // When we defaulted to Default SDK auth, only STS-assume if "Directories to search"
            // supplied a target ARN that looks like an IAM role (not ClientMachine, which the
            // orchestrator framework fills with the agent identifier like "UOLinux"). Otherwise
            // use the host's ambient credentials as-is — the case where the EC2 instance role
            // already holds the needed Organizations/ACM permissions and there's nothing to assume.
            bool dirsHasRoleArn = !string.IsNullOrWhiteSpace(dirsValue)
                && (dirsValue.StartsWith("arn:aws:iam:", StringComparison.OrdinalIgnoreCase)
                    || dirsValue.StartsWith("arn:aws-us-gov:iam:", StringComparison.OrdinalIgnoreCase)
                    || dirsValue.StartsWith("arn:aws-cn:iam:", StringComparison.OrdinalIgnoreCase));
            if (defaultedAuth)
            {
                customFields.DefaultSdkAssumeRole = dirsHasRoleArn;
                Logger.LogDebug($"DefaultSdkAssumeRole resolved to {customFields.DefaultSdkAssumeRole} based on 'Directories to search'.");
            }

            if (jobConfiguration.JobProperties != null)
            {
                foreach (var kv in jobConfiguration.JobProperties)
                {
                    Logger.LogDebug($"JobProperties[{kv.Key}] = {kv.Value}");
                }
            }
            Logger.LogInformation($"Discovery base (management) credentials will assume role: {roleArn}");

            AuthenticationParameters authParams = new AuthenticationParameters
            {
                RoleARN = roleArn,
                Region = baseRegion,
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
                Logger.LogError("An error occurred while trying to get AWS Credentials.");
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = jobConfiguration.JobHistoryId,
                    FailureMessage = ex.Message
                };
            }
            string authMethod = AuthDescription.DescribeMethod(customFields, roleArn);
            Logger.LogInformation($"AWS credential method resolved: [{authMethod}] (base role/profile: {roleArn}, region: {baseRegion}).");
            Logger.LogTrace("AWS Credentials resolved. Performing Discovery.");

            return PerformDiscovery(providedCredentials, jobConfiguration, submitDiscovery);
        }

        internal JobResult PerformDiscovery(AwsExtensionCredential awsCredentials,
            DiscoveryJobConfiguration config, SubmitDiscoveryUpdate submitDiscovery)
        {
            Logger.MethodEntry();
            var warnings = new List<string>();   // specific account/region targets that failed to scan
            int accountsScanned = 0;
            var discoveredStores = new List<string>();

            // ----- Resolve discovery inputs -----
            // Base region drives the partition (commercial / GovCloud / China) and credential resolution.
            string baseRegionName = GetJobProperty(config.JobProperties, "Region") ?? "us-east-1";
            RegionEndpoint baseRegion = RegionEndpoint.GetBySystemName(baseRegionName);

            // The IAM role name to assume in each member account. Operators may override via an explicit
            // "DiscoveryRoleName" job property, or via the "File name patterns to match" dialog field
            // (packed as "patterns"/"namepatterns"); otherwise the manifest default is used.
            string discoveryRoleName = FirstNonEmpty(
                GetJobPropertyMulti(config.JobProperties, "DiscoveryRoleName", "patterns", "namepatterns"),
                DefaultDiscoveryRoleName);

            // Optional sts:ExternalId for the cross-account AssumeRole calls. No standard dialog field maps
            // to this, so it can only be supplied programmatically via an "ExternalId" job property.
            string externalId = GetJobProperty(config.JobProperties, "ExternalId");

            // Optional comma-separated list of account IDs to skip. Operators may supply it via an explicit
            // "IgnoredAccounts" job property or the "Directories to ignore" dialog field ("ignoreddirs").
            var ignoredAccounts = new HashSet<string>(
                SplitCsv(GetJobPropertyMulti(config.JobProperties, "IgnoredAccounts", "ignoreddirs", "DirectoriesToIgnore")),
                StringComparer.OrdinalIgnoreCase);

            // Regions to scan. Explicit "Regions" job property wins; otherwise the "Extensions" dialog field
            // ("extensions") can carry a comma-separated list; otherwise every region in the base partition.
            // When the operator pins a list we use it verbatim; otherwise (blank or "*") we auto-detect the
            // enabled regions per account below so each account is scanned against its own enabled set.
            string regionsConfig = GetJobPropertyMulti(config.JobProperties, "Regions", "extensions");
            bool autoDetectRegions = string.IsNullOrWhiteSpace(regionsConfig) || regionsConfig.Trim() == "*";
            var regions = ParseRegions(regionsConfig, baseRegion.PartitionName);

            Logger.LogDebug($"Discovery candidate region set: {regions.Count} region(s) in partition '{baseRegion.PartitionName}'. " +
                            $"Per-account auto-detection: {autoDetectRegions}. Member-account role name: '{discoveryRoleName}'.");

            // ----- Enumerate accounts via AWS Organizations -----
            // If the base credentials belong to an Organizations management/delegated account with
            // organizations:ListAccounts, we enumerate every account and probe each. If that fails (not an
            // org, AccessDenied, single-account setup, etc.) we fall back to scanning the base account only,
            // so existing single-account discovery keeps working.
            List<string> accountIds = TryListOrganizationAccounts(awsCredentials.GetAwsCredentialObject(), baseRegion);

            if (accountIds == null)
            {
                Logger.LogInformation("AWS Organizations enumeration unavailable; falling back to single-account discovery " +
                                      "using the resolved base credentials.");
                accountsScanned = 1;
                var baseRegions = ResolveAccountRegions(awsCredentials.GetAwsCredentialObject(), regions,
                    autoDetectRegions, baseRegion, "Base account");
                ScanRegionsSingleAccount(awsCredentials.GetAwsCredentialObject(), baseRegions, discoveredStores, warnings);
            }
            else
            {
                Logger.LogInformation($"AWS Organizations returned {accountIds.Count} active account(s). Beginning cross-account scan.");
                foreach (string accountId in accountIds)
                {
                    if (ignoredAccounts.Contains(accountId))
                    {
                        Logger.LogDebug($"Skipping account {accountId} (in ignore list).");
                        continue;
                    }

                    string targetRoleArn = BuildMemberRoleArn(baseRegion.PartitionName, accountId, discoveryRoleName);

                    AWSCredentials targetCreds;
                    try
                    {
                        targetCreds = AssumeRole(awsCredentials.GetAwsCredentialObject(), baseRegion, targetRoleArn, externalId);
                    }
                    catch (Exception ex)
                    {
                        // Account may not have the discovery role deployed yet, or trust isn't configured.
                        Logger.LogWarning($"Could not assume {targetRoleArn} in account {accountId}: {ex.Message}");
                        warnings.Add($"account {accountId} (could not assume {targetRoleArn}: {ex.Message})");
                        continue;
                    }

                    accountsScanned++;

                    // Detect the regions enabled in this specific account (unless the operator pinned a list).
                    var accountRegions = ResolveAccountRegions(targetCreds, regions, autoDetectRegions, baseRegion,
                        $"Account {accountId}");

                    foreach (var region in accountRegions)
                    {
                        try
                        {
                            if (RegionHasCertificates(targetCreds, region))
                            {
                                string storePath = StorePathParser.Build(targetRoleArn, region.SystemName);
                                Logger.LogInformation($"Discovered ACM store in account {accountId} region {region.SystemName}. " +
                                                $"Discovered store path: {storePath}");
                                discoveredStores.Add(storePath);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Region disabled in this account, missing ACM permission, etc. Log and continue.
                            Logger.LogWarning($"Could not check ACM in account {accountId} region {region.SystemName}: {ex.Message}");
                            warnings.Add($"account {accountId} region {region.SystemName} ({ex.Message})");
                        }
                    }
                }
            }

            bool crossAccount = accountIds != null;
            string scope = crossAccount
                ? $"scanned {accountsScanned} of {accountIds.Count} organization account(s)"
                : "scanned the base account (AWS Organizations enumeration was unavailable)";
            string summary = $"Discovery complete: {scope} and found {discoveredStores.Count} ACM certificate store(s) with certificates, " +
                "now reported to Keyfactor Command.";
            if (warnings.Count > 0)
            {
                int show = Math.Min(warnings.Count, 5);
                string affected = string.Join("; ", warnings.Take(show));
                if (warnings.Count > show)
                    affected += $"; and {warnings.Count - show} more";
                summary += $" {warnings.Count} target(s) could not be scanned: {affected}.";
            }

            Logger.LogInformation(summary);
            submitDiscovery.Invoke(discoveredStores);

            if (warnings.Count > 0)
            {
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Warning,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage = summary
                };
            }

            // JobResult carries only FailureMessage; Command shows it as the job message regardless of
            // status, so populate it on success to surface the discovery outcome in Command.
            return new JobResult
            {
                Result = OrchestratorJobStatusJobResult.Success,
                JobHistoryId = config.JobHistoryId,
                FailureMessage = summary
            };
        }

        // Single-account fallback: scan regions using the supplied credentials directly. Emits legacy
        // region-only store paths so the operator supplies the Role ARN via ClientMachine on approval
        // (preserving pre-cross-account behavior).
        private void ScanRegionsSingleAccount(AWSCredentials credentials, List<RegionEndpoint> regions,
            List<string> discoveredStores, List<string> warnings)
        {
            foreach (var region in regions)
            {
                try
                {
                    if (RegionHasCertificates(credentials, region))
                    {
                        Logger.LogInformation($"Discovered ACM store in region {region.SystemName}. Adding to discovered stores.");
                        discoveredStores.Add(region.SystemName);
                    }
                    else
                    {
                        Logger.LogDebug($"No ACM certificates found in region {region.SystemName}.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Could not check ACM in region {region.SystemName}: {ex.Message}");
                    warnings.Add($"region {region.SystemName} ({ex.Message})");
                }
            }
        }

        // Returns true if the account/region has at least one ACM certificate.
        private bool RegionHasCertificates(AWSCredentials credentials, RegionEndpoint region)
        {
            Logger.LogDebug($"Checking for ACM certificates in region: {region.SystemName}");
            using var acm = new AmazonCertificateManagerClient(credentials, region);
            var request = new ListCertificatesRequest { MaxItems = 1 };
            var response = AsyncHelpers.RunSync(() => acm.ListCertificatesAsync(request));
            return response.CertificateSummaryList.Count > 0;
        }

        // Enumerates all ACTIVE account IDs in the organization. Returns null if Organizations is
        // unavailable (not a management/delegated account, AccessDenied, single-account setup, etc.),
        // signalling the caller to fall back to single-account discovery.
        private List<string> TryListOrganizationAccounts(AWSCredentials credentials, RegionEndpoint baseRegion)
        {
            try
            {
                // Organizations is a global service reachable only from the partition's home region.
                RegionEndpoint orgRegion = GetOrganizationsRegion(baseRegion.PartitionName);
                using var org = new AmazonOrganizationsClient(credentials, orgRegion);

                var ids = new List<string>();
                string next = null;
                do
                {
                    var req = new ListAccountsRequest { NextToken = next };
                    var resp = AsyncHelpers.RunSync(() => org.ListAccountsAsync(req));
                    ids.AddRange(resp.Accounts
                        .Where(a => a.Status == AccountStatus.ACTIVE)
                        .Select(a => a.Id));
                    next = resp.NextToken;
                } while (!string.IsNullOrEmpty(next));

                return ids;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"organizations:ListAccounts unavailable ({ex.Message}); cross-account enumeration will be skipped.");
                return null;
            }
        }

        // STS AssumeRole using the management credentials. The AWS SDK applies exponential backoff on
        // throttling (e.g. STS rate limits) by default.
        private AWSCredentials AssumeRole(AWSCredentials baseCredentials, RegionEndpoint baseRegion,
            string targetRoleArn, string externalId)
        {
            using var sts = new AmazonSecurityTokenServiceClient(baseCredentials, baseRegion);
            var req = new AssumeRoleRequest
            {
                RoleArn = targetRoleArn,
                RoleSessionName = "KeyfactorACMDiscovery"
            };
            if (!string.IsNullOrWhiteSpace(externalId))
            {
                req.ExternalId = externalId;
            }

            var resp = AsyncHelpers.RunSync(() => sts.AssumeRoleAsync(req));
            return new SessionAWSCredentials(
                resp.Credentials.AccessKeyId,
                resp.Credentials.SecretAccessKey,
                resp.Credentials.SessionToken);
        }

        // Builds arn:aws:iam::<accountId>:role/<roleName>, partition-aware for GovCloud/China.
        private static string BuildMemberRoleArn(string partitionName, string accountId, string roleName)
        {
            string partition;
            switch (partitionName)
            {
                case "aws-us-gov": partition = "aws-us-gov"; break;
                case "aws-cn": partition = "aws-cn"; break;
                default: partition = "aws"; break;
            }
            return $"arn:{partition}:iam::{accountId}:role/{roleName}";
        }

        // AWS Organizations has a single global endpoint per partition.
        private static RegionEndpoint GetOrganizationsRegion(string partitionName)
        {
            switch (partitionName)
            {
                case "aws-us-gov": return RegionEndpoint.GetBySystemName("us-gov-west-1");
                case "aws-cn": return RegionEndpoint.GetBySystemName("cn-northwest-1");
                default: return RegionEndpoint.GetBySystemName("us-east-1");
            }
        }

        // Resolves which regions to scan for one account. When the operator pinned an explicit region list
        // it is used verbatim. Otherwise we call ec2:DescribeRegions with that account's own credentials and
        // intersect with the partition list, so each account is scanned against its own enabled regions.
        // If DescribeRegions is unavailable in the account (no ec2:DescribeRegions permission, etc.) we scan
        // the full candidate list and let any disabled region fail/skip gracefully per-region.
        private List<RegionEndpoint> ResolveAccountRegions(AWSCredentials credentials, List<RegionEndpoint> candidateRegions,
            bool autoDetect, RegionEndpoint baseRegion, string accountLabel)
        {
            if (!autoDetect)
                return candidateRegions;

            var enabled = GetEnabledRegions(credentials, baseRegion);
            if (enabled == null)
                return candidateRegions;

            var filtered = candidateRegions.Where(r => enabled.Contains(r.SystemName)).ToList();
            Logger.LogDebug($"{accountLabel}: filtered candidate regions {candidateRegions.Count} -> {filtered.Count} enabled.");
            return filtered;
        }

        private HashSet<string> GetEnabledRegions(AWSCredentials credentials, RegionEndpoint baseRegion)
        {
            try
            {
                using var ec2 = new AmazonEC2Client(credentials, baseRegion);
                var response = AsyncHelpers.RunSync(() => ec2.DescribeRegionsAsync(new DescribeRegionsRequest { AllRegions = false }));
                return new HashSet<string>(response.Regions.Select(r => r.RegionName), StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"ec2:DescribeRegions failed ({ex.Message}); will scan the full partition list and warn on any opt-in regions.");
                return null;
            }
        }

        private List<RegionEndpoint> ParseRegions(string regionsConfig, string partitionName)
        {
            // Treat blank or "*" (spec's wildcard for "all enabled regions") as "use the full partition list".
            if (string.IsNullOrWhiteSpace(regionsConfig) || regionsConfig.Trim() == "*")
                return RegionEndpoint.EnumerableAllRegions
                    .Where(r => r.PartitionName == partitionName)
                    .ToList();

            return SplitCsv(regionsConfig)
                .Select(r => RegionEndpoint.GetBySystemName(r))
                .ToList();
        }

        private static IEnumerable<string> SplitCsv(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Enumerable.Empty<string>();

            return value
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0);
        }

        private static string FirstNonEmpty(string a, string b)
        {
            return string.IsNullOrWhiteSpace(a) ? b : a;
        }

        private string GetJobProperty(Dictionary<string, object> jobProperties, string key)
        {
            if (jobProperties != null && jobProperties.TryGetValue(key, out object value) && value != null)
                return value.ToString();
            return null;
        }

        // Case-insensitive lookup across several candidate keys (returns the first non-empty match).
        // Lets a single concept be supplied either via an explicit programmatic property or via the
        // standard Discovery dialog field whose JobProperties key varies by Command version.
        private string GetJobPropertyMulti(Dictionary<string, object> jobProperties, params string[] keys)
        {
            if (jobProperties == null) return null;
            foreach (var key in keys)
            {
                var match = jobProperties
                    .Where(kv => kv.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                    .Select(kv => kv.Value?.ToString())
                    .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
                if (!string.IsNullOrWhiteSpace(match))
                    return match;
            }
            return null;
        }
    }
}
