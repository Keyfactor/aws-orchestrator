using System;

using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.Internal.Util;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;

using Keyfactor.AnyAgent.AwsCertificateManager.Models;

namespace Keyfactor.AnyAgent.AwsCertificateManager
{
	public static class Utilities
	{
		public static Credentials AwsAuthenticateWithWebIdentity(AuthResponse authResponse, RegionEndpoint endpoint, string awsAccount, string awsRole)
		{
			Credentials credentials = null;
			try
			{
				var account = awsAccount;
				var stsClient = new AmazonSecurityTokenServiceClient(new AnonymousAWSCredentials());
				var assumeRequest = new AssumeRoleWithWebIdentityRequest
				{
					WebIdentityToken = authResponse?.AccessToken,
					RoleArn = $"arn:aws:iam::{account}:role/{awsRole}",
					RoleSessionName = "KeyfactorSession",
					DurationSeconds = Convert.ToInt32(authResponse?.ExpiresIn)
				};

				var assumeResult = AsyncHelpers.RunSync(() => stsClient.AssumeRoleWithWebIdentityAsync(assumeRequest));
				credentials = assumeResult.Credentials;
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}

			return credentials;
		}

		public static Credentials AwsAuthenticate(string accessKey, string accessSecret, string sessionToken, RegionEndpoint endpoint, string awsAccount, string awsRole)
		{
			Credentials credentials = null;
			try
			{
				var account = awsAccount;
				var stsClient = new AmazonSecurityTokenServiceClient(accessKey, accessSecret, sessionToken);
				var assumeRequest = new AssumeRoleRequest
				{
					RoleArn = $"arn:aws:iam::{account}:role/{awsRole}",
					RoleSessionName = "KeyfactorSession"
				};

				var assumeResult = AsyncHelpers.RunSync(() => stsClient.AssumeRoleAsync(assumeRequest));
				credentials = assumeResult.Credentials;
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}

			return credentials;
		}
	}
}