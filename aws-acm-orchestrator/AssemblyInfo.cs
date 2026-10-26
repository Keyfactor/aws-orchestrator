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

using System.Runtime.CompilerServices;

// The job classes expose their collaborators (AcmClient, Logger) and their pure helpers as
// 'internal' precisely so they can be exercised without reaching AWS. Grant the unit test
// assembly access rather than widening anything to public.
[assembly: InternalsVisibleTo("Keyfactor.Extensions.Orchestrator.Aws.Acm.Tests")]

// Moq generates proxies for internal interfaces (e.g. IAmazonCertificateManager members) in a
// dynamic assembly, which needs the same grant.
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
