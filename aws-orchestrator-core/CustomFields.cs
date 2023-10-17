using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
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
}