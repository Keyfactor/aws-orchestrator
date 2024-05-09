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
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.AnyAgent.AwsCertificateManager.Jobs.ACM
{
    public class Management : Jobs.Management, IManagementJobExtension
    {
        public string ExtensionName => "AWS-ACM";

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
            ACMCustomFields customFields = JsonConvert.DeserializeObject<ACMCustomFields>(jobConfiguration.CertificateStoreDetails.Properties,
                    new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });
            Logger.LogTrace($"Populated ACMCustomFields: {JsonConvert.SerializeObject(customFields)}");

            Logger.LogTrace("Resolving AWS Credentials object.");
            Credentials providedCredentials = AuthUtilities.GetCredentials(customFields, jobConfiguration, jobConfiguration.CertificateStoreDetails);
            Logger.LogTrace("AWS Credentials resolved.");

            // perform add or remove
            if (jobConfiguration.OperationType.ToString() == "Add")
            {
                Logger.LogTrace("Performing Management Add.");
                return PerformAddition(providedCredentials, jobConfiguration);
            }
            else if (jobConfiguration.OperationType.ToString() == "Remove")
            {
                Logger.LogTrace("Performing Management Remove.");
                return PerformRemoval(providedCredentials, jobConfiguration);
            }
            else
            {
                Logger.LogError($"Unrecognized Management Operation Type: {jobConfiguration.OperationType}");
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = jobConfiguration.JobHistoryId,
                    FailureMessage = "Invalid Management Operation"
                };
            }
        }
    }
}
