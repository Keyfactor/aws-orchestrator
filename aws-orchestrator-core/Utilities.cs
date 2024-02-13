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
		public static Credentials AwsAuthenticateWithWebIdentity(AuthResponse authResponse, string awsAccount, string awsRole)
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
				throw;
			}

			return credentials;
		}

		public static Credentials AwsAuthenticate(string accessKey, string accessSecret, string awsAccount, string awsRole)
		{
			Credentials credentials = null;
			try
			{
				var account = awsAccount;
				// STS needed when using user's credentials, to assume a role
				// NOT NEEDED? when using role credentials from EC2
				var stsClient = new AmazonSecurityTokenServiceClient(accessKey, accessSecret);
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
				throw;
			}

			return credentials;
		}

		public static Credentials DefaultAuthenticate(string awsAccount, string awsRole)
        {
			// DEFAULT SHOULD NOT ASSUME ROLE
			Credentials credentials = null;
			try
            {
				var account = awsAccount;
				var stsClient = new AmazonSecurityTokenServiceClient();
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
				throw;
			}

			return credentials;
		}
	}
}