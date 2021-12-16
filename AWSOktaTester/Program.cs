using System;
using System.Text;
using Amazon;
using Amazon.CertificateManager;
using Amazon.Runtime;
using Amazon.Runtime.Internal.Util;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using AWSOktaTester.Models;
using Newtonsoft.Json;
using RestSharp;

namespace AWSOktaTester
{
    internal class Program
    {
        private static void Main()
        {
            var regionList =
                "us-east-2,us-east-1,us-west-1,us-west-2,af-south-1,ap-east-1,ap-south-1,ap-northeast-3,ap-northeast-2,ap-southeast-1,ap-southeast-2,ap-northeast-1,ca-central-1,eu-central-1,eu-west-1,eu-west-2,eu-south-1,eu-west-3,eu-north-1,me-south-1,sa-east-1";

            var client = new RestClient("https://dev-21576506.okta.com/oauth2/default/v1/token");
            client.Timeout = -1;
            var request = new RestRequest(Method.POST);
            request.AddHeader("Accept", "application/json");
            var clientId = "0oa2o4rjvecIR9LSs5d7";
            var clientSecret = "cEqCWy7MuxfnnYef8aCKqYlDcOg2HI1NzOdf4ijw";

            var plainTextBytes = Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}");
            var authHeader = Convert.ToBase64String(plainTextBytes);

            request.AddHeader("Authorization", $"Basic {authHeader}");
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddParameter("grant_type", "client_credentials");
            request.AddParameter("scope", "GetCertificates");
            var response = client.Execute(request);
            Console.WriteLine(response.Content);

            var authResponse = JsonConvert.DeserializeObject<AuthResponse>(response.Content);

            foreach (var region in regionList.Split(','))
            {

                try
                {
                    var endPoint = RegionEndpoint.GetBySystemName(region);
                    var credentials = AwsAuthenticate(authResponse, endPoint);

                    IAmazonCertificateManager acmClient = new AmazonCertificateManagerClient(credentials.AccessKeyId,
                        credentials.SecretAccessKey, region: RegionEndpoint.GetBySystemName(region),
                        awsSessionToken: credentials.SessionToken);
                    var certList = AsyncHelpers.RunSync(() => acmClient.ListCertificatesAsync());
                    Console.Write($"Found {certList.CertificateSummaryList.Count} Certificates\n");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }

        private static Credentials AwsAuthenticate(AuthResponse authResponse, RegionEndpoint endpoint)
        {
            Credentials credentials = null;
            try
            {
                var account = "140977833822";
                var stsClient = new AmazonSecurityTokenServiceClient(new AnonymousAWSCredentials());
                var assumeRequest = new AssumeRoleWithWebIdentityRequest
                {
                    WebIdentityToken = authResponse?.AccessToken,
                    RoleArn = $"arn:aws:iam::{account}:role/OKTAJDCertMan",
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
    }
}