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
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.AnyAgent.AwsCertificateManager.Jobs.ACM
{
    public class Inventory : Jobs.Inventory, IInventoryJobExtension
    {
        public string ExtensionName => "AWS-ACM";

        public Inventory(ILogger<Inventory> logger)
        {
            Logger = logger;
        }

        public JobResult ProcessJob(InventoryJobConfiguration jobConfiguration, SubmitInventoryUpdate submitInventoryUpdate)
        {
            // TODO: validate presence of required parameters based on auth type selected
            ACMCustomFields customFields = JsonConvert.DeserializeObject<ACMCustomFields>(jobConfiguration.CertificateStoreDetails.Properties,
                    new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });

            Credentials providedCredentials = AuthUtilities.GetCredentials(Logger, customFields, jobConfiguration, jobConfiguration.CertificateStoreDetails);

            return base.PerformInventory(providedCredentials, jobConfiguration, submitInventoryUpdate);
        }
    }
}
