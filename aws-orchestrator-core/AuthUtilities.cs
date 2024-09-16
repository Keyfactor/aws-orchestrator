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
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;

using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Keyfactor.AnyAgent.AwsCertificateManager
{
	public class AuthUtilities
	{
        private readonly ILogger _logger;
        private readonly IPAMSecretResolver _pam;

        public AuthUtilities(IPAMSecretResolver pam, ILogger logger)
        {
            _pam = pam;
            _logger = logger;
        }

		public Credentials GetCredentials(ACMCustomFields customFields, JobConfiguration jobConfiguration, CertificateStore certStore)
		{
            _logger.MethodEntry();
            _logger.LogDebug("Selecting credential method.");
			
            if (customFields.UseIAM)
            {
                _logger.LogInformation("Using IAM User authentication method for creating AWS Credentials.");
                var accessKey = ResolvePamField(jobConfiguration.ServerUsername, "ServerUsername (IAM AccessKey)");
                var accessSecret = ResolvePamField(jobConfiguration.ServerPassword, "ServerPassword (IAM AccessSecret)");

                string awsRole = customFields.IAMAssumeRole;
                _logger.LogDebug($"Assuming AWS Role - {awsRole}");

                string awsAccountId = certStore.ClientMachine;
                _logger.LogDebug($"Using AWS Account ID - {awsAccountId} - from the ClientMachine field");

                _logger.LogTrace("Attempting to authenticate with AWS using IAM access credentials.");
                return AwsAuthenticate(accessKey, accessSecret, awsAccountId, awsRole);
            }
            else if (customFields.UseOAuth)
            {
                _logger.LogInformation("Using OAuth authenticaiton method for creating AWS Credentials.");
                var clientId = ResolvePamField(jobConfiguration.ServerUsername, "ServerUsername (OAuth Client ID)");
                var clientSecret = ResolvePamField(jobConfiguration.ServerPassword, "ServerPassword (OAuth Client Secret)");
                OAuthParameters oauthParams = new OAuthParameters()
                {
                    OAuthUrl = customFields.OAuthUrl,
                    GrantType = customFields.OAuthGrantType,
                    Scope = customFields.OAuthScope,
                    ClientId = clientId,
                    ClientSecret = clientSecret
                };

                _logger.LogTrace("Attempting to authenticate with OAuth provider.");
                OAuthResponse authResponse = OAuthAuthenticate(oauthParams);
                _logger.LogTrace("Received OAuth response.");

                string awsRole = customFields.OAuthAssumeRole;
                _logger.LogDebug($"Assuming AWS Role - {awsRole}");

                string awsAccountId = certStore.ClientMachine;
                _logger.LogDebug($"Using AWS Account ID - {awsAccountId} - from the ClientMachine field");

                _logger.LogTrace("Attempting to authenticate with AWS using OAuth response.");
                return AwsAuthenticateWithWebIdentity(authResponse, awsAccountId, awsRole);
            }
            else // use default SDK credential resolution
            {
                _logger.LogInformation("Using default AWS SDK credential resolution for creating AWS Credentials.");
                return null;
            }
        }

		public Credentials AwsAuthenticateWithWebIdentity(OAuthResponse authResponse, string awsAccount, string awsRole)
		{
            _logger.MethodEntry();
			Credentials credentials = null;
			try
			{
				var account = awsAccount;
                _logger.LogTrace($"Using AWS Account - {account}");
				var stsClient = new AmazonSecurityTokenServiceClient(new AnonymousAWSCredentials());
                _logger.LogTrace("Created AWS STS client with anonymous credentials.");
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
                _logger.LogDebug($"Prepared Assume Role With Web Identity request with fields: {logAssumeRequest}");

                _logger.LogTrace("Submitting Assume Role With Web Identity request.");
				var assumeResult = AsyncHelpers.RunSync(() => stsClient.AssumeRoleWithWebIdentityAsync(assumeRequest));
                _logger.LogTrace("Received response to Assume Role With Web Identity request.");
				credentials = assumeResult.Credentials;
			}
			catch (Exception e)
			{
                _logger.LogError($"Error Occurred in AwsAuthenticateWithWebIdentity: {e.Message}");

                throw;
			}

			return credentials;
		}

		public Credentials AwsAuthenticate(string accessKey, string accessSecret, string awsAccount, string awsRole)
		{
            _logger.MethodEntry();
			Credentials credentials = null;
			try
			{
				var account = awsAccount;
                _logger.LogTrace($"Using AWS Account - {account}");
                var stsClient = new AmazonSecurityTokenServiceClient(accessKey, accessSecret);
                _logger.LogTrace("Created AWS STS client with IAM user credentials.");
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
                _logger.LogDebug($"Prepared Assume Role request with fields: {logAssumeRequest}");

                _logger.LogTrace("Submitting Assume Role request.");
                var assumeResult = AsyncHelpers.RunSync(() => stsClient.AssumeRoleAsync(assumeRequest));
                _logger.LogTrace("Received response to Assume Role request.");
				credentials = assumeResult.Credentials;
			}
			catch (Exception e)
			{
                _logger.LogError($"Error Occurred in AwsAuthenticate: {e.Message}");
				throw;
			}

			return credentials;
		}

		public OAuthResponse OAuthAuthenticate(OAuthParameters parameters)
        {
            try
            {
                _logger.MethodEntry();
                _logger.LogTrace($"Creating RestClient with OAuth URL: {parameters.OAuthUrl}");

                var client = new RestClient(parameters.OAuthUrl)
                {
                    Timeout = -1
                };

                if (client.BaseUrl.Scheme != "https")
                {
                    var errorMessage = $"OAuth server needs to use HTTPS scheme but does not: {parameters.OAuthUrl}";
                    _logger.LogError(errorMessage);
                    throw new Exception(errorMessage);
                }

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
                _logger.LogDebug($"Prepared Rest Request: {logHttpRequest}");

                _logger.LogTrace("Executing Rest request.");
                var response = client.Execute(request);
                _logger.LogTrace("Received responst to Rest request to OAUth");
                var authResponse = JsonConvert.DeserializeObject<OAuthResponse>(response.Content);
                _logger.LogTrace("Deserialized OAuthResponse.");
                return authResponse;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occurred in OAuthAuthenticate: {e.Message}");
                throw;
            }
        }

        public string ResolvePamField(string field, string fieldName)
        {
            if (_pam != null)
            {
                _logger.LogDebug($"Attempting to resolve PAM-eligible field - {fieldName}");
                return _pam.Resolve(field);
            }
            else
            {
                _logger.LogTrace($"PAM-eigible field {fieldName} was not resolved via PAM as no IPAMSecretResolver was present.");
                return field;
            }
        }
	}
}