// Copyright 2025 Keyfactor
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

using Keyfactor.Extensions.Aws.Models;

namespace Keyfactor.Extensions.Orchestrator.Aws.Acm.Jobs
{
    /// <summary>
    /// Produces the canonical name of the AWS credential method a store/job is configured for,
    /// derived from its auth custom fields. This lets every job log <em>which</em> authentication
    /// path it actually took (in the same vocabulary the auth library uses) instead of leaving the
    /// operator to infer it, and gives automated tests a stable, greppable marker.
    ///
    /// A Credential-Profile store is distinguished from a plain Default-SDK store by the "[profile]"
    /// prefix the auth library expects on the role/ARN value.
    /// </summary>
    internal static class AuthDescription
    {
        public static string DescribeMethod(AuthCustomFieldParameters cf, string roleArn)
        {
            if (cf == null)
                return "Unknown";

            if (cf.UseOAuth)
                return "OAuthProvider";

            if (cf.UseIAM)
                return "IamUser";

            if (cf.UseDefaultSdkAuth)
            {
                bool usesProfile = !string.IsNullOrWhiteSpace(roleArn) && roleArn.TrimStart().StartsWith("[");
                string method = usesProfile ? "DefaultSdk_CredentialProfile" : "DefaultSdk";
                if (cf.DefaultSdkAssumeRole)
                    method += "_AssumeRole";
                return method;
            }

            return "Unknown";
        }
    }
}
