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

using Amazon.SecurityToken.Model;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;

namespace Keyfactor.AnyAgent.AwsCertificateManager.Jobs.IAM
{
	public class Management : Jobs.Management, IManagementJobExtension
	{
		public string ExtensionName => "AWSCerManA";

		protected internal virtual IAMCustomFields CustomFields { get; set; }

		public Management(IPAMSecretResolver pam, ILogger<Management> logger)
		{
			PamSecretResolver = pam;
			Logger = logger;
			AuthUtilities = new AuthUtilities(pam, logger);
		}

		public JobResult ProcessJob(ManagementJobConfiguration jobConfiguration)
		{
            Logger.LogWarning("This Management job is running for a deprecated store type 'AWSCerManA'. Consider migrating to the supported store type.");
            Logger.MethodEntry();
            Logger.LogTrace($"Deserializing Cert Store Properties: {jobConfiguration.CertificateStoreDetails.Properties}");
            CustomFields = JsonConvert.DeserializeObject<IAMCustomFields>(jobConfiguration.CertificateStoreDetails.Properties,
	new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });
            Logger.LogTrace($"Populated IAMCustomFields: {JsonConvert.SerializeObject(CustomFields)}");

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

                Logger.LogWarning("This Management job completed running for a deprecated store type 'AWSCerManA'. Consider migrating to the supported store type.");
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
				Logger.LogTrace("Resolving AWS Credentials.");
                string accessKey = AuthUtilities.ResolvePamField(config.ServerUsername, "ServerUsername (IAM AccessKey)");
                string accessSecret = AuthUtilities.ResolvePamField(config.ServerPassword, "ServerPassword (IAM AccessSecret)");
                Credentials credentials = AuthUtilities.AwsAuthenticate(accessKey, accessSecret, config.CertificateStoreDetails.StorePath, CustomFields.AwsRole);
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
				Logger.LogTrace("Resolving AWS Credentials.");
                string accessKey = AuthUtilities.ResolvePamField(config.ServerUsername, "ServerUsername (IAM AccessKey)");
                string accessSecret = AuthUtilities.ResolvePamField(config.ServerPassword, "ServerPassword (IAM AccessSecret)");
                Credentials credentials = AuthUtilities.AwsAuthenticate(accessKey, accessSecret, config.CertificateStoreDetails.StorePath, CustomFields.AwsRole);
				Logger.LogTrace("Resolved AWS Credentials. Perfoming Management Remove.");

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
	}
}