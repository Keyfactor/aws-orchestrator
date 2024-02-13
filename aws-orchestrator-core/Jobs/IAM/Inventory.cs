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
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Keyfactor.AnyAgent.AwsCertificateManager.Jobs.IAM
{
	public class Inventory : Jobs.Inventory, IInventoryJobExtension
	{
		public string ExtensionName => "AWSCerManA";

		private readonly ILogger<Inventory> _logger;

		public Inventory(ILogger<Inventory> logger) =>
			_logger = logger;

		protected internal virtual IAMCustomFields CustomFields { get; set; }

		protected internal virtual ListCertificatesResponse AllCertificates { get; set; }
		protected internal virtual GetCertificateRequest GetCertificateRequest { get; set; }
		protected internal virtual GetCertificateResponse GetCertificateResponse { get; set; }

		public JobResult ProcessJob(InventoryJobConfiguration jobConfiguration, SubmitInventoryUpdate submitInventoryUpdate)
		{
			base.Logger = _logger;
			_logger.MethodEntry();

			CustomFields = JsonConvert.DeserializeObject<IAMCustomFields>(jobConfiguration.CertificateStoreDetails.Properties,
					new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });

			return PerformInventory(jobConfiguration, submitInventoryUpdate);
		}

		private JobResult PerformInventory(InventoryJobConfiguration config, SubmitInventoryUpdate siu)
		{
			_logger.MethodEntry();
			try
			{
				Credentials credentials = Utilities.AwsAuthenticate(config.ServerUsername, config.ServerPassword, config.CertificateStoreDetails.StorePath, CustomFields.AwsRole);
				_logger.LogTrace($"Credentials JSON: {JsonConvert.SerializeObject(credentials)}");

				return base.PerformInventory(credentials, CustomFields, config, siu);
			}
			catch (Exception e)
			{
				_logger.LogError(e.Message);
				throw;
			}
		}
	}
}