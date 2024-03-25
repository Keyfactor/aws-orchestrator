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

using System;
using Amazon.SecurityToken.Model;
using Keyfactor.AnyAgent.AwsCertificateManager.Models;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.AnyAgent.AwsCertificateManager.Jobs.Okta
{
	public class Management : Jobs.Management, IManagementJobExtension
	{
		public string ExtensionName => "AWSCerManO";

		protected internal virtual OktaCustomFields CustomFields { get; set; }

		public Management(IPAMSecretResolver pam, ILogger<Management> logger)
		{
			PamSecretResolver = pam;
			Logger = logger;
			AuthUtilities = new AuthUtilities(pam, logger);
		}

		public JobResult ProcessJob(ManagementJobConfiguration jobConfiguration)
		{
			Logger.MethodEntry();
            Logger.LogTrace($"Deserializing Cert Store Properties: {jobConfiguration.CertificateStoreDetails.Properties}");
            CustomFields = JsonConvert.DeserializeObject<OktaCustomFields>(jobConfiguration.CertificateStoreDetails.Properties,
				new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });
            Logger.LogTrace($"Populated OktaCustomFields: {JsonConvert.SerializeObject(CustomFields)}");

            return PerformManagement(jobConfiguration);
		}

		private JobResult PerformManagement(ManagementJobConfiguration config)
		{
			try
			{
				Logger.MethodEntry();
				var complete = new JobResult
				{
					Result = OrchestratorJobStatusJobResult.Failure,
					JobHistoryId = config.JobHistoryId,
					FailureMessage =
						"Invalid Management Operation"
				};

				if (config.OperationType.ToString() == "Add")
				{
					Logger.LogTrace($"Adding...");
					complete = PerformAddition(config);
				}
				else if (config.OperationType.ToString() == "Remove")
				{
					Logger.LogTrace($"Removing...");
					complete = PerformRemoval(config);
				}

				return complete;
			}
			catch (Exception e)
			{
				Logger.LogError($"Error Occurred in Management.PerformManagement: {e.Message}");
				throw;
			}
		}

		private JobResult PerformAddition(ManagementJobConfiguration config)
		{
			try
			{
				Logger.MethodEntry();
				Logger.LogTrace("Authenticating with Okta.");
				OAuthResponse authResponse = OktaAuthenticate(config);
                Logger.LogTrace("Received Okta auth response. Resolving AWS Credentials.");

                Credentials credentials = AuthUtilities.AwsAuthenticateWithWebIdentity(authResponse, config.CertificateStoreDetails.StorePath, CustomFields.AwsRole);
				Logger.LogTrace("Resolved AWS Credentials. Performing Management Add.");

				return PerformAddition(credentials, config);
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

		private JobResult PerformRemoval(ManagementJobConfiguration config)
		{
			try
			{
				Logger.MethodEntry();
				Logger.LogTrace("Authenticating with Okta.");
				OAuthResponse authResponse = OktaAuthenticate(config);
                Logger.LogTrace("Received Okta auth response. Resolving AWS Credentials.");

                Credentials credentials = AuthUtilities.AwsAuthenticateWithWebIdentity(authResponse, config.CertificateStoreDetails.StorePath, CustomFields.AwsRole);
				Logger.LogTrace("Resolved AWS Credentials. Performing Management Remove.");

				return PerformRemoval(credentials, config);
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

		private OAuthResponse OktaAuthenticate(ManagementJobConfiguration config)
		{
            var oktaAuthUrl = $"https://{config.CertificateStoreDetails.ClientMachine}{CustomFields.OAuthPath}";
            var clientId = AuthUtilities.ResolvePamField(config.ServerUsername, "ServerUsername (Okta Client ID)");
            var clientSecret = AuthUtilities.ResolvePamField(config.ServerPassword, "ServerPassword (Okta Client Secret)");
            var grantType = CustomFields.GrantType;
            var scope = CustomFields.Scope;

            Logger.LogTrace("Creating OAuthParameters from Okta store type parameters.");

            OAuthParameters oauthParameters = new OAuthParameters
            {
                OAuthUrl = oktaAuthUrl,
                ClientId = clientId,
                ClientSecret = clientSecret,
                GrantType = grantType,
                Scope = scope
            };

            return AuthUtilities.OAuthAuthenticate(oauthParameters);
        }
	}
}