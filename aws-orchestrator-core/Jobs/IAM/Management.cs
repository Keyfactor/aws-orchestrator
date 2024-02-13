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
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.AnyAgent.AwsCertificateManager.Jobs.IAM
{
	public class Management : Jobs.Management, IManagementJobExtension
	{
		public string ExtensionName => "AWSCerManA";

		protected internal virtual IAMCustomFields CustomFields { get; set; }

		private readonly ILogger<Management> _logger;

		public Management(ILogger<Management> logger) =>
			_logger = logger;

		public JobResult ProcessJob(ManagementJobConfiguration jobConfiguration)
		{
			CustomFields = JsonConvert.DeserializeObject<IAMCustomFields>(jobConfiguration.CertificateStoreDetails.Properties,
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

				Credentials credentials = Utilities.AwsAuthenticate(config.ServerUsername, config.ServerPassword, config.CertificateStoreDetails.StorePath, CustomFields.AwsRole);
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

				Credentials credentials = Utilities.AwsAuthenticate(config.ServerUsername, config.ServerPassword, config.CertificateStoreDetails.StorePath, CustomFields.AwsRole);
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
	}
}