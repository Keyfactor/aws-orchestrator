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
using System.Text;
using Amazon.SecurityToken.Model;
using Keyfactor.AnyAgent.AwsCertificateManager.Models;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

using RestSharp;

namespace Keyfactor.AnyAgent.AwsCertificateManager.Jobs.Okta
{
	public class Management : Jobs.Management, IManagementJobExtension
	{
		public string ExtensionName => "AWSCerManO";

		protected internal virtual OktaCustomFields CustomFields { get; set; }

		private readonly ILogger<Management> _logger;

		public Management(ILogger<Management> logger) =>
			_logger = logger;

		public JobResult ProcessJob(ManagementJobConfiguration jobConfiguration)
		{
			CustomFields = JsonConvert.DeserializeObject<OktaCustomFields>(jobConfiguration.CertificateStoreDetails.Properties,
	new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });
			return PerformManagement(jobConfiguration);
		}

		private JobResult PerformManagement(ManagementJobConfiguration config)
		{
			try
			{
				_logger.MethodEntry();
				var complete = new JobResult
				{
					Result = OrchestratorJobStatusJobResult.Failure,
					JobHistoryId = config.JobHistoryId,
					FailureMessage =
						"Invalid Management Operation"
				};

				if (config.OperationType.ToString() == "Add")
				{
					_logger.LogTrace($"Adding...");
					complete = PerformAddition(config);
				}
				else if (config.OperationType.ToString() == "Remove")
				{
					_logger.LogTrace($"Removing...");
					complete = PerformRemoval(config);
				}

				return complete;
			}
			catch (Exception e)
			{
				_logger.LogError($"Error Occurred in Management.PerformManagement: {e.Message}");
				throw;
			}
		}

		private JobResult PerformAddition(ManagementJobConfiguration config)
		{
			try
			{
				_logger.MethodEntry();
				AuthResponse authResponse = OktaAuthenticate(config);
				_logger.LogTrace($"Got authResponse: {JsonConvert.SerializeObject(authResponse)}");

				Credentials credentials = Utilities.AwsAuthenticateWithWebIdentity(authResponse, config.CertificateStoreDetails.StorePath, CustomFields.AwsRole);
				_logger.LogTrace($"Credentials JSON: {JsonConvert.SerializeObject(credentials)}");

				return base.PerformAddition(credentials, config);
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
				_logger.MethodEntry();

				AuthResponse authResponse = OktaAuthenticate(config);
				_logger.LogTrace($"Got authResponse: {JsonConvert.SerializeObject(authResponse)}");

				Credentials credentials = Utilities.AwsAuthenticateWithWebIdentity(authResponse, config.CertificateStoreDetails.StorePath, CustomFields.AwsRole);
				_logger.LogTrace($"Credentials JSON: {JsonConvert.SerializeObject(credentials)}");

				return base.PerformRemoval(credentials, config);
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

		private AuthResponse OktaAuthenticate(ManagementJobConfiguration config)
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
	}
}