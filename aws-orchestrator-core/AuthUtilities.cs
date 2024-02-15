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
using System.Text;
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.Internal.Util;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Keyfactor.AnyAgent.AwsCertificateManager.Models;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Org.BouncyCastle.Crypto.Parameters;
using RestSharp;

using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Keyfactor.AnyAgent.AwsCertificateManager
{
	public static class AuthUtilities
	{
		public static Credentials AwsAuthenticateWithWebIdentity(OAuthResponse authResponse, string awsAccount, string awsRole)
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

		public static OAuthResponse OktaAuthenticate(OAuthParameters parameters)
        {
            try
            {
                //logger.MethoEntry();
                //logger.LogTrace($"Okta Auth URL: {parameters.OAuthUrl}");

                var client =
                    new RestClient(parameters.OAuthUrl)
                    {
                        Timeout = -1
                    };
                var request = new RestRequest(Method.POST);
                request.AddHeader("Accept", "application/json");
                var clientId = parameters.ClientId;
                var clientSecret = parameters.ClientSecret;
                var plainTextBytes = Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}");
                var authHeader = Convert.ToBase64String(plainTextBytes);
                request.AddHeader("Authorization", $"Basic {authHeader}");
                request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                request.AddParameter("grant_type", parameters.GrantType);
                request.AddParameter("scope", parameters.Scope);
                var response = client.Execute(request);
                var authResponse = JsonConvert.DeserializeObject<OAuthResponse>(response.Content);

                return authResponse;
            }
            catch (Exception e)
            {
                //logger.LogError($"Error Occurred in Inventory.OktaAuthenticate: {e.Message}");
                throw;
            }
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