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
            ACMCustomFields customFields = JsonConvert.DeserializeObject<ACMCustomFields>(jobConfiguration.CertificateStoreDetails.Properties,
                    new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });

            Credentials providedCredentials = AuthUtilities.GetCredentials(customFields, jobConfiguration, jobConfiguration.CertificateStoreDetails);

            // perform add or remove
            if (jobConfiguration.OperationType.ToString() == "Add")
            {
                //_logger.LogTrace($"Adding...");
                return PerformAddition(providedCredentials, jobConfiguration);
            }
            else if (jobConfiguration.OperationType.ToString() == "Remove")
            {
                //_logger.LogTrace($"Removing...");
                return PerformRemoval(providedCredentials, jobConfiguration);
            }
            else
            {
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
