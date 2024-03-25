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
using Amazon.Runtime;
using Amazon.Runtime.Internal.Util;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Keyfactor.AnyAgent.AwsCertificateManager.Models;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;

using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Keyfactor.AnyAgent.AwsCertificateManager
{
	public static class AuthUtilities
	{
		public static Credentials GetCredentials(ILogger logger, ACMCustomFields customFields, JobConfiguration jobConfiguration, CertificateStore certStore)
		{
            logger.MethodEntry();
            logger.LogDebug("Selecting credential method.");
			string awsRole = certStore.ClientMachine;
            logger.LogDebug($"Using AWS Role - {awsRole} - from the ClientMachine field");
            if (customFields.UseIAM)
            {
                logger.LogInformation("Using IAM User authentication method for creating AWS Credentials.");
                var accessKey = jobConfiguration.ServerUsername;
                var accessSecret = jobConfiguration.ServerPassword;

                logger.LogTrace("Attempting to authenticate with AWS using IAM access credentials.");
                return AwsAuthenticate(logger, accessKey, accessSecret, customFields.IamAccountId, awsRole);
            }
            else if (customFields.UseOAuth)
            {
                logger.LogInformation("Using OAuth authenticaiton method for creating AWS Credentials.");
                var clientId = jobConfiguration.ServerUsername;
                var clientSecret = jobConfiguration.ServerPassword;
                OAuthParameters oauthParams = new OAuthParameters()
                {
                    OAuthUrl = customFields.OAuthUrl,
                    GrantType = customFields.OAuthGrantType,
                    Scope = customFields.OAuthScope,
                    ClientId = clientId,
                    ClientSecret = clientSecret
                };

                logger.LogTrace("Attempting to authenticate with OAuth provider.");
                OAuthResponse authResponse = OAuthAuthenticate(logger, oauthParams);
                logger.LogTrace("Received OAuth response.");

                logger.LogTrace("Attempting to authenticate with AWS using OAuth response.");
                return AwsAuthenticateWithWebIdentity(logger, authResponse, customFields.OAuthAccountId, awsRole);
            }
            else // use default SDK credential resolution
            {
                logger.LogInformation("Using default AWS SDK credential resolution for creating AWS Credentials.");
                return null;
            }
        }

		public static Credentials AwsAuthenticateWithWebIdentity(ILogger logger, OAuthResponse authResponse, string awsAccount, string awsRole)
		{
            logger.MethodEntry();
			Credentials credentials = null;
			try
			{
				var account = awsAccount;
                logger.LogTrace($"Using AWS Account - {account}");
				var stsClient = new AmazonSecurityTokenServiceClient(new AnonymousAWSCredentials());
                logger.LogTrace("Created AWS STS client with anonymous credentials.");
				var assumeRequest = new AssumeRoleWithWebIdentityRequest
				{
					WebIdentityToken = authResponse?.AccessToken,
					RoleArn = $"arn:aws:iam::{account}:role/{awsRole}",
					RoleSessionName = "KeyfactorSession",
					DurationSeconds = Convert.ToInt32(authResponse?.ExpiresIn)
				};
                var logAssumeRequest = new
                {
                    WebIdentityToken = "**redacted**",
                    assumeRequest.RoleArn,
                    assumeRequest.RoleSessionName,
                    assumeRequest.DurationSeconds
                };
                logger.LogDebug($"Prepared Assume Role With Web Identity request with fields: {logAssumeRequest}");

                logger.LogTrace("Submitting Assume Role With Web Identity request.");
				var assumeResult = AsyncHelpers.RunSync(() => stsClient.AssumeRoleWithWebIdentityAsync(assumeRequest));
                logger.LogTrace("Received response to Assume Role With Web Identity request.");
				credentials = assumeResult.Credentials;
			}
			catch (Exception e)
			{
                logger.LogError($"Error Occurred in AwsAuthenticateWithWebIdentity: {e.Message}");

                throw;
			}

			return credentials;
		}

		public static Credentials AwsAuthenticate(ILogger logger, string accessKey, string accessSecret, string awsAccount, string awsRole)
		{
            logger.MethodEntry();
			Credentials credentials = null;
			try
			{
				var account = awsAccount;
                logger.LogTrace($"Using AWS Account - {account}");
                var stsClient = new AmazonSecurityTokenServiceClient(accessKey, accessSecret);
                logger.LogTrace("Created AWS STS client with IAM user credentials.");
				var assumeRequest = new AssumeRoleRequest
				{
					RoleArn = $"arn:aws:iam::{account}:role/{awsRole}",
					RoleSessionName = "KeyfactorSession"
				};

                var logAssumeRequest = new
                {
                    assumeRequest.RoleArn,
                    assumeRequest.RoleSessionName
                };
                logger.LogDebug($"Prepared Assume Role request with fields: {logAssumeRequest}");

                logger.LogTrace("Submitting Assume Role request.");
                var assumeResult = AsyncHelpers.RunSync(() => stsClient.AssumeRoleAsync(assumeRequest));
                logger.LogTrace("Received response to Assume Role request.");
				credentials = assumeResult.Credentials;
			}
			catch (Exception e)
			{
                logger.LogError($"Error Occurred in AwsAuthenticate: {e.Message}");
				throw;
			}

			return credentials;
		}

		public static OAuthResponse OAuthAuthenticate(ILogger logger, OAuthParameters parameters)
        {
            try
            {
                logger.MethodEntry();
                logger.LogTrace($"Creating RestClient with OAuth URL: {parameters.OAuthUrl}");

                var client = new RestClient(parameters.OAuthUrl)
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

                var logHttpRequest = new
                {
                    Method = "POST",
                    AcceptHeader = "application/json",
                    AuthorizationHeader = "Basic **redacted**",
                    ContentTypeHeader = "application/x-www-form-urlencoded",
                    grant_type = parameters.GrantType,
                    scope = parameters.Scope
                };
                logger.LogDebug($"Prepared Rest Request: {logHttpRequest}");

                logger.LogTrace("Executing Rest request.");
                var response = client.Execute(request);
                logger.LogTrace("Received responst to Rest request to OAUth");
                var authResponse = JsonConvert.DeserializeObject<OAuthResponse>(response.Content);
                logger.LogTrace("Deserialized OAuthResponse.");
                return authResponse;
            }
            catch (Exception e)
            {
                logger.LogError($"Error Occurred in OAuthAuthenticate: {e.Message}");
                throw;
            }
        }
	}
}