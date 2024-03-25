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

namespace Keyfactor.AnyAgent.AwsCertificateManager.Jobs.ACM
{
    public class Inventory : Jobs.Inventory, IInventoryJobExtension
    {
        public string ExtensionName => "AWS-ACM";

        public Inventory(IPAMSecretResolver pam, ILogger<Inventory> logger)
        {
            PamSecretResolver = pam;
            Logger = logger;
            AuthUtilities = new AuthUtilities(pam, logger);
        }

        public JobResult ProcessJob(InventoryJobConfiguration jobConfiguration, SubmitInventoryUpdate submitInventoryUpdate)
        {
            Logger.MethodEntry();
            Logger.LogTrace($"Deserializing Cert Store Properties: {jobConfiguration.CertificateStoreDetails.Properties}");
            ACMCustomFields customFields = JsonConvert.DeserializeObject<ACMCustomFields>(jobConfiguration.CertificateStoreDetails.Properties,
                    new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });
            Logger.LogTrace($"Populated ACMCustomFields: {JsonConvert.SerializeObject(customFields)}");

            Logger.LogTrace("Resolving AWS Credentials object.");
            Credentials providedCredentials = AuthUtilities.GetCredentials(customFields, jobConfiguration, jobConfiguration.CertificateStoreDetails);
            
            Logger.LogTrace("AWS Credentials resolved. Performing Inventory.");
            return PerformInventory(providedCredentials, jobConfiguration, submitInventoryUpdate);
        }
    }
}
