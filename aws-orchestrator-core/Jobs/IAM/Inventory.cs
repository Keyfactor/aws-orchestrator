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
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;

namespace Keyfactor.AnyAgent.AwsCertificateManager.Jobs.IAM
{
	public class Inventory : Jobs.Inventory, IInventoryJobExtension
	{
		public string ExtensionName => "AWSCerManA";

		public Inventory(IPAMSecretResolver pam, ILogger<Inventory> logger)
		{
			PamSecretResolver = pam;
			Logger = logger;
			AuthUtilities = new AuthUtilities(pam, logger);
		}

		protected internal virtual IAMCustomFields CustomFields { get; set; }

		public JobResult ProcessJob(InventoryJobConfiguration jobConfiguration, SubmitInventoryUpdate submitInventoryUpdate)
		{
			Logger.MethodEntry();
            Logger.LogTrace($"Deserializing Cert Store Properties: {jobConfiguration.CertificateStoreDetails.Properties}");
            CustomFields = JsonConvert.DeserializeObject<IAMCustomFields>(jobConfiguration.CertificateStoreDetails.Properties,
					new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });
            Logger.LogTrace($"Populated IAMCustomFields: {JsonConvert.SerializeObject(CustomFields)}");

            return PerformInventory(jobConfiguration, submitInventoryUpdate);
		}

		private JobResult PerformInventory(InventoryJobConfiguration config, SubmitInventoryUpdate siu)
		{
			Logger.MethodEntry();
			try
			{
				Logger.LogTrace("Resolving AWS Credentials.");
                Credentials credentials = AuthUtilities.AwsAuthenticate(config.ServerUsername, config.ServerPassword, config.CertificateStoreDetails.StorePath, CustomFields.AwsRole);
				Logger.LogTrace("Resolved AWS Credentials. Performing Inventory.");

				return PerformInventory(credentials, config, siu);
			}
			catch (Exception e)
			{
				Logger.LogError(e.Message);
				throw;
			}
		}
	}
}