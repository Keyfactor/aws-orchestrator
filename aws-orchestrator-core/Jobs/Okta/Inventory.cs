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

using Amazon;
using Amazon.CertificateManager;
using Amazon.CertificateManager.Model;
using Amazon.Runtime.Internal.Util;
using Amazon.SecurityToken.Model;
using Keyfactor.AnyAgent.AwsCertificateManager.Models;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Keyfactor.AnyAgent.AwsCertificateManager.Jobs.Okta
{
	public class Inventory : Jobs.Inventory, IInventoryJobExtension
	{
		public string ExtensionName => "AWSCerManO";

		private readonly ILogger<Inventory> _logger;

		public Inventory(ILogger<Inventory> logger) =>
			_logger = logger;

		protected internal virtual OktaCustomFields CustomFields { get; set; }

		protected internal virtual ListCertificatesResponse AllCertificates { get; set; }
		protected internal virtual GetCertificateRequest GetCertificateRequest { get; set; }
		protected internal virtual GetCertificateResponse GetCertificateResponse { get; set; }

		public JobResult ProcessJob(InventoryJobConfiguration jobConfiguration, SubmitInventoryUpdate submitInventoryUpdate)
		{
			base.Logger = _logger;
			_logger.MethodEntry();

			CustomFields = JsonConvert.DeserializeObject<OktaCustomFields>(jobConfiguration.CertificateStoreDetails.Properties,
					new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });

			return PerformInventory(jobConfiguration, submitInventoryUpdate);
		}

		private JobResult PerformInventory(InventoryJobConfiguration config, SubmitInventoryUpdate siu)
		{
			_logger.MethodEntry();
			try
			{
				OAuthResponse authResponse = OktaAuthenticate(config);
				_logger.LogTrace($"Got authResponse: {JsonConvert.SerializeObject(authResponse)}");

                Credentials credentials = AuthUtilities.AwsAuthenticateWithWebIdentity(Logger, authResponse, config.CertificateStoreDetails.StorePath, CustomFields.AwsRole);
                _logger.LogTrace($"Credentials JSON: {JsonConvert.SerializeObject(credentials)}");

				return base.PerformInventory(credentials, config, siu);
			}
			catch (Exception e)
			{
				_logger.LogError(e.Message);
				throw;
			}
		}

		private OAuthResponse OktaAuthenticate(InventoryJobConfiguration config)
		{
			try
			{
				_logger.MethodEntry();
				var oktaAuthUrl = $"https://{config.CertificateStoreDetails.ClientMachine}{CustomFields.OAuthPath}";
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
				var authResponse = JsonConvert.DeserializeObject<OAuthResponse>(response.Content);
				_logger.LogTrace($"Okta Serialized Auth Response: {JsonConvert.SerializeObject(authResponse)}");

				return authResponse;
			}
			catch (Exception e)
			{
				_logger.LogError($"Error Occurred in Inventory.OktaAuthenticate: {e.Message}");
				throw;
			}
		}
	}
}