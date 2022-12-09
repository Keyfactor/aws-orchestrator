using System;
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
	}

	public class IAMCustomFields : CustomFields
	{
	}
}