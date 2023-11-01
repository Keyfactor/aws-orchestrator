// Copyright 2023 Keyfactor
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

using Newtonsoft.Json;

namespace Keyfactor.AnyAgent.AwsCertificateManager.Models
{
    public class AuthResponse
    {
        [JsonProperty("token_type", NullValueHandling = NullValueHandling.Ignore)] public string TokenType { get; set; }
        [JsonProperty("expires_in", NullValueHandling = NullValueHandling.Ignore)] public int ExpiresIn { get; set; }
        [JsonProperty("access_token", NullValueHandling = NullValueHandling.Ignore)] public string AccessToken { get; set; }
        [JsonProperty("scope", NullValueHandling = NullValueHandling.Ignore)] public string Scope { get; set; }
    }
}