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

using System;

namespace Keyfactor.Extensions.Orchestrator.Aws.Acm.Jobs
{
    /// <summary>
    /// A certificate store's identity is the AWS Role ARN to assume (which embeds the target
    /// account) plus the AWS Region. Historically these lived in two separate fields:
    /// ClientMachine held the Role ARN and StorePath held the Region.
    ///
    /// Cross-account Discovery can only return a list of StorePath strings (it has no channel to
    /// set ClientMachine per discovered store), so discovered stores fold both values into a single
    /// self-contained StorePath of the form "<roleArn>|<region>". '|' cannot appear in an ARN or a
    /// region name, so it is an unambiguous separator.
    ///
    /// This helper parses both shapes so legacy (manually-created) stores and new (discovered)
    /// stores resolve through one code path.
    /// </summary>
    internal static class StorePathParser
    {
        internal const char Separator = '|';

        /// <summary>
        /// Resolves the Role ARN and Region for a store.
        /// New/discovered stores: StorePath = "<roleArn>|<region>" — both come from StorePath.
        /// Legacy stores: StorePath = "<region>" and the Role ARN comes from ClientMachine.
        /// </summary>
        public static (string RoleArn, string Region) Parse(string storePath, string clientMachine)
        {
            if (!string.IsNullOrWhiteSpace(storePath) && storePath.IndexOf(Separator) >= 0)
            {
                string[] parts = storePath.Split(new[] { Separator }, 2);
                string role = parts[0].Trim();
                string region = parts.Length > 1 ? parts[1].Trim() : string.Empty;

                // An empty role segment falls back to ClientMachine so a hand-edited combined path
                // like "|us-east-1" still works.
                return (string.IsNullOrWhiteSpace(role) ? clientMachine : role, region);
            }

            // Legacy shape: region in StorePath, role in ClientMachine.
            return (clientMachine, storePath?.Trim());
        }

        /// <summary>
        /// Builds the self-contained StorePath emitted by Discovery for a discovered store.
        /// </summary>
        public static string Build(string roleArn, string region)
        {
            return $"{roleArn}{Separator}{region}";
        }
    }
}
