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

using Amazon.SecurityToken.Model;
using Keyfactor.AnyAgent.AwsCertificateManager.Models;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;

namespace Keyfactor.AnyAgent.AwsCertificateManager.Jobs.Okta
{
	public class Inventory : Jobs.Inventory, IInventoryJobExtension
	{
		public string ExtensionName => "AWSCerManO";

		public Inventory(IPAMSecretResolver pam, ILogger<Inventory> logger)
		{
			PamSecretResolver = pam;
			Logger = logger;
			AuthUtilities = new AuthUtilities(pam, logger);
		}

		protected internal virtual OktaCustomFields CustomFields { get; set; }

		public JobResult ProcessJob(InventoryJobConfiguration jobConfiguration, SubmitInventoryUpdate submitInventoryUpdate)
		{
            Logger.LogWarning("This Inventory job is running for a deprecated store type 'AWSCerManO'. Consider migrating to the supported store type.");
            Logger.MethodEntry();
            Logger.LogTrace($"Deserializing Cert Store Properties: {jobConfiguration.CertificateStoreDetails.Properties}");
            CustomFields = JsonConvert.DeserializeObject<OktaCustomFields>(jobConfiguration.CertificateStoreDetails.Properties,
					new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });
            Logger.LogTrace($"Populated OktaCustomFields: {JsonConvert.SerializeObject(CustomFields)}");

            return PerformInventory(jobConfiguration, submitInventoryUpdate);
		}

		private JobResult PerformInventory(InventoryJobConfiguration config, SubmitInventoryUpdate siu)
		{
			Logger.MethodEntry();
			try
			{
				Logger.LogTrace("Authenticating with Okta.");
				OAuthResponse authResponse = OktaAuthenticate(config);

				Logger.LogTrace("Received Okta auth response. Resolving AWS Credentials.");
                Credentials credentials = AuthUtilities.AwsAuthenticateWithWebIdentity(authResponse, config.CertificateStoreDetails.StorePath, CustomFields.AwsRole);
				Logger.LogTrace("Resolved AWS Credentials. Performing Inventory.");

				var result = PerformInventory(credentials, config, siu);
                Logger.LogWarning("This Inventory job completed running for a deprecated store type 'AWSCerManO'. Consider migrating to the supported store type.");
				return result;
            }
			catch (Exception e)
			{
				Logger.LogError(e.Message);
				throw;
			}
		}

		private OAuthResponse OktaAuthenticate(InventoryJobConfiguration config)
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