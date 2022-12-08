using System;
using System.ComponentModel;

using Newtonsoft.Json;

namespace Keyfactor.AnyAgent.AwsCertificateManager
{
	public class CustomFields
	{
		[JsonProperty("auth_type")]
		[DefaultValue(false)]
		public string AuthenticationType { get; set; }

		[JsonProperty("grant_type")]
		[DefaultValue(false)]
		public string GrantType { get; set; }

		[JsonProperty("scope")]
		[DefaultValue(false)]
		public string Scope { get; set; }

		[JsonProperty("session_token")]
		[DefaultValue(false)]
		public string SessionToken { get; set; }

		[JsonProperty("awsregions")]
		[DefaultValue(false)]
		public string AwsRegions { get; set; }

		[JsonProperty("awsrole")]
		[DefaultValue(false)]
		public string AwsRole { get; set; }

		[JsonIgnore]
		public AuthType AuthType
		{
			get
			{
				if (AuthenticationType.Contains("okta", System.StringComparison.OrdinalIgnoreCase))
				{
					return AuthType.Okta;
				}
				else if (AuthenticationType.Contains("aws", System.StringComparison.OrdinalIgnoreCase))
				{
					return AuthType.IAM;
				}
				else
				{
					throw new Exception("Invalid authentication type. Valid options are Okta OAuth, AWS IAM");
				}
			}
		}
	}

	public enum AuthType
	{
		Okta = 0,
		IAM = 1
	}
}