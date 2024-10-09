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

using System.ComponentModel;
using Newtonsoft.Json;

namespace Keyfactor.AnyAgent.AwsCertificateManager
{
	public class CustomFields
	{
		[JsonProperty("awsregions")]
		[DefaultValue(false)]
		public string AwsRegions { get; set; }

		[JsonProperty("awsrole")]
		[DefaultValue(false)]
		public string AwsRole { get; set; }
	}

	public class OktaCustomFields : CustomFields
	{
		[JsonProperty("grant_type")]
		[DefaultValue(false)]
		public string GrantType { get; set; }

		[JsonProperty("scope")]
		[DefaultValue(false)]
		public string Scope { get; set; }

        [JsonProperty("oauthpath")]
        [DefaultValue("/oauth2/default/v1/token")]
        public string OAuthPath { get; set; }
	}

	public class IAMCustomFields : CustomFields
	{
	}

	public class ACMCustomFields
	{
		[JsonProperty("UseOAuth")]
		[DefaultValue(false)]
		public bool UseOAuth { get; set; }

		[JsonProperty("UseIAM")]
		[DefaultValue(false)]
		public bool UseIAM { get; set; }

		[JsonProperty("OAuthAssumeRole")]
		public string OAuthAssumeRole { get; set; }

		[JsonProperty("OAuthScope")]
		public string OAuthScope { get; set; }

		[JsonProperty("OAuthGrantType")]
		public string OAuthGrantType { get; set; }

		[JsonProperty("OAuthUrl")]
		public string OAuthUrl { get; set; }

		[JsonProperty("IAMAssumeRole")]
		public string IAMAssumeRole { get; set; }
	}
}