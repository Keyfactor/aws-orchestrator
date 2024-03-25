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
using Keyfactor.AnyAgent.AwsCertificateManager.Models;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.AnyAgent.AwsCertificateManager.Jobs.ACM
{
    public class Management : Jobs.Management, IManagementJobExtension
    {
        public string ExtensionName => "AWS-ACM";

        public Management(ILogger<Management> logger)
        {
            Logger = logger;
        }

        public JobResult ProcessJob(ManagementJobConfiguration jobConfiguration)
        {
            // TODO: validate presence of required parameters based on auth type selected
            ACMCustomFields customFields = JsonConvert.DeserializeObject<ACMCustomFields>(jobConfiguration.CertificateStoreDetails.Properties,
                    new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });

            Credentials providedCredentials = AuthUtilities.GetCredentials(Logger, customFields, jobConfiguration, jobConfiguration.CertificateStoreDetails);

            // perform add or remove
            if (jobConfiguration.OperationType.ToString() == "Add")
            {
                //_logger.LogTrace($"Adding...");
                return base.PerformAddition(providedCredentials, jobConfiguration);
            }
            else if (jobConfiguration.OperationType.ToString() == "Remove")
            {
                //_logger.LogTrace($"Removing...");
                return base.PerformRemoval(providedCredentials, jobConfiguration);
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
