using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace awstestbed
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			var region1 = RegionEndpoint.USEast1;
			var region2 = RegionEndpoint.USEast2;

			string accessKeyId = "AKIATGWFWW6RXYPFWYG5";
			string secretAccessKey = "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";

			string accountId = "220531701667";
			string role = "CertManTest";

			var stsClient = new AmazonSecurityTokenServiceClient(accessKeyId, secretAccessKey);

			var assumeRequest = new AssumeRoleRequest
			{
				RoleArn = $"arn:aws:iam::{accountId}:role/{role}",
				RoleSessionName = "KeyfactorSession"
			};

			var assumeResult = stsClient.AssumeRole(assumeRequest);
		}
	}
}