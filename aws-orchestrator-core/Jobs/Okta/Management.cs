﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

using Amazon;
using Amazon.CertificateManager;
using Amazon.CertificateManager.Model;
using Amazon.Runtime.Internal.Util;
using Amazon.SecurityToken.Model;

using Keyfactor.AnyAgent.AwsCertificateManager.Models;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;

using RestSharp;

namespace Keyfactor.AnyAgent.AwsCertificateManager.Jobs.Okta
{
	public class Management : IManagementJobExtension
	{
		public string ExtensionName => "AWSCerManO";

		private static String certStart = "-----BEGIN CERTIFICATE-----\n";
		private static String certEnd = "\n-----END CERTIFICATE-----";

		// ReSharper disable once FieldCanBeMadeReadOnly.Local
		private static Func<String, String> _pemify = (ss => ss.Length <= 64 ? ss : ss.Substring(0, 64) + "\n" + _pemify(ss.Substring(64)));

		protected internal virtual IAmazonCertificateManager AcmClient { get; set; }
		protected internal virtual DescribeCertificateResponse DescribeCertificateResponse { get; set; }
		protected internal virtual ImportCertificateResponse IcrResponse { get; set; }
		protected internal virtual DeleteCertificateResponse DeleteResponse { get; set; }
		protected internal virtual AsymmetricKeyEntry KeyEntry { get; set; }
		protected internal virtual OktaCustomFields CustomFields { get; set; }

		private readonly ILogger<Management> _logger;

		public Management(ILogger<Management> logger) =>
			_logger = logger;

		public JobResult ProcessJob(ManagementJobConfiguration jobConfiguration)
		{
			CustomFields = JsonConvert.DeserializeObject<OktaCustomFields>(jobConfiguration.CertificateStoreDetails.Properties,
	new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });
			return PerformManagement(jobConfiguration);
		}

		private JobResult PerformManagement(ManagementJobConfiguration config)
		{
			try
			{
				_logger.MethodEntry();
				var complete = new JobResult
				{
					Result = OrchestratorJobStatusJobResult.Failure,
					JobHistoryId = config.JobHistoryId,
					FailureMessage =
						"Invalid Management Operation"
				};

				if (config.OperationType.ToString() == "Add")
				{
					_logger.LogTrace($"Adding...");
					complete = PerformAddition(config);
				}
				else if (config.OperationType.ToString() == "Remove")
				{
					_logger.LogTrace($"Removing...");
					complete = PerformRemoval(config);
				}

				return complete;
			}
			catch (Exception e)
			{
				_logger.LogError($"Error Occurred in Management.PerformManagement: {e.Message}");
				throw;
			}
		}

		private JobResult PerformAddition(ManagementJobConfiguration config)
		{
			//Temporarily only performing additions
			try
			{
				_logger.MethodEntry();
				AuthResponse authResponse = OktaAuthenticate(config);
				_logger.LogTrace($"Got authResponse: {JsonConvert.SerializeObject(authResponse)}");

				var endPoint = RegionEndpoint.GetBySystemName(config.JobProperties["AWS Region"].ToString());
				_logger.LogTrace($"Got Endpoint From Job Properties JSON: {JsonConvert.SerializeObject(endPoint)}");

				Credentials credentials = Utilities.AwsAuthenticateWithWebIdentity(authResponse, endPoint, config.CertificateStoreDetails.StorePath, CustomFields.AwsRole);

				_logger.LogTrace($"Credentials JSON: {JsonConvert.SerializeObject(credentials)}");

				using (AcmClient = new AmazonCertificateManagerClient(credentials.AccessKeyId,
					credentials.SecretAccessKey, region: endPoint,
					awsSessionToken: credentials.SessionToken))
				{
					_logger.LogTrace($"AcmClient JSON: {JsonConvert.SerializeObject(AcmClient)}");
					if (!String.IsNullOrWhiteSpace(config.JobCertificate.PrivateKeyPassword)) // This is a PFX Entry
					{
						_logger.LogTrace($"Found Private Key {config.JobCertificate.PrivateKeyPassword}");
						if (!String.IsNullOrWhiteSpace(config.JobCertificate.Alias))
						{
							_logger.LogTrace($"No Alias Found");
							//ARN Provided, Verify It is Not A PCA/Amazon Issued Cert
							DescribeCertificateResponse = AsyncHelpers.RunSync(() => AcmClient.DescribeCertificateAsync(config.JobCertificate.Alias));
							_logger.LogTrace($"DescribeCertificateResponse JSON: {JsonConvert.SerializeObject(DescribeCertificateResponse)}");

							if (DescribeCertificateResponse.Certificate.Type != CertificateType.IMPORTED)
							{
								_logger.LogTrace($"Non User Imported Certificate Type Found");
								return new JobResult
								{
									Result = OrchestratorJobStatusJobResult.Failure,
									JobHistoryId = config.JobHistoryId,
									FailureMessage =
										"Amazon Web Services Certificate Manager only supports overwriting user-imported certificates.\"), \"Management/Add"
								};
							}
						}

						// Load PFX
						byte[] pfxBytes = Convert.FromBase64String(config.JobCertificate.Contents);
						Pkcs12Store p;
						using (var pfxBytesMemoryStream = new MemoryStream(pfxBytes))
						{
							p = new Pkcs12Store(pfxBytesMemoryStream, config.JobCertificate.PrivateKeyPassword.ToCharArray());
						}
						_logger.LogTrace($"Created Pkcs12Store");

						// Extract private key
						String alias;
						String privateKeyString;
						using (MemoryStream memoryStream = new MemoryStream())
						{
							using (TextWriter streamWriter = new StreamWriter(memoryStream))
							{
								PemWriter pemWriter = new PemWriter(streamWriter);

								alias = (p.Aliases.Cast<String>()).SingleOrDefault(a => p.IsKeyEntry(a));
								AsymmetricKeyParameter publicKey = p.GetCertificate(alias).Certificate.GetPublicKey();

								KeyEntry = p.GetKey(alias);//Don't really need alias?
								if (KeyEntry == null)
								{
									throw new Exception("Unable to retrieve private key");
								}

								AsymmetricKeyParameter privateKey = KeyEntry.Key;
								AsymmetricCipherKeyPair keyPair = new AsymmetricCipherKeyPair(publicKey, privateKey);

								pemWriter.WriteObject(keyPair.Private);
								streamWriter.Flush();
								privateKeyString = Encoding.ASCII.GetString(memoryStream.GetBuffer()).Trim().Replace("\r", "").Replace("\0", "");
								_logger.LogTrace($"Got Private Key String {privateKeyString}");
								memoryStream.Close();
								streamWriter.Close();
							}
						}

						String certPem = certStart + _pemify(Convert.ToBase64String(p.GetCertificate(alias).Certificate.GetEncoded())) + certEnd;
						_logger.LogTrace($"Got certPem {certPem}");
						//Create Memory Stream For Server Cert
						ImportCertificateRequest icr;
						using (MemoryStream serverCertStream = CertStringToStream(certPem))
						{
							using (MemoryStream privateStream = CertStringToStream(privateKeyString))
							{
								using (MemoryStream chainStream = GetChain(p, alias))
								{
									icr = new ImportCertificateRequest
									{
										Certificate = serverCertStream,
										PrivateKey = privateStream,
										CertificateChain = chainStream
									};
								}
							}
						}
						icr.CertificateArn = config.JobCertificate.Alias?.Length >= 20 ? config.JobCertificate.Alias.Trim() : null; //If an arn is provided, use it, this will perform a renewal/replace
						_logger.LogTrace($"Certificate arn {icr.CertificateArn}");

						IcrResponse = AsyncHelpers.RunSync(() => AcmClient.ImportCertificateAsync(icr));
						_logger.LogTrace($"IcrResponse JSON: {JsonConvert.SerializeObject(IcrResponse)}");
						// Ensure 200 Response
						if (IcrResponse.HttpStatusCode == HttpStatusCode.OK)
						{
							_logger.LogTrace($"Return Success");
							return new JobResult
							{
								Result = OrchestratorJobStatusJobResult.Success,
								JobHistoryId = config.JobHistoryId,
								FailureMessage = ""
							};
						}
						else
						{
							_logger.LogTrace($"Return Failure");
							return new JobResult
							{
								Result = OrchestratorJobStatusJobResult.Failure,
								JobHistoryId = config.JobHistoryId,
								FailureMessage =
									"Management/Add"
							};
						}
					}
					else  // Non-PFX
					{
						_logger.LogTrace($"Return PFX Failure");
						return new JobResult
						{
							Result = OrchestratorJobStatusJobResult.Failure,
							JobHistoryId = config.JobHistoryId,
							FailureMessage =
								"Certificate Must be a PFX"
						};
					}
				}
			}
			catch (Exception e)
			{
				return new JobResult
				{
					Result = OrchestratorJobStatusJobResult.Failure,
					JobHistoryId = config.JobHistoryId,
					FailureMessage =
						$"Management/Add {e.Message}"
				};
			}
		}

		private JobResult PerformRemoval(ManagementJobConfiguration config)
		{
			//Temporarily only performing additions
			try
			{
				_logger.MethodEntry();

				AuthResponse authResponse = OktaAuthenticate(config);
				_logger.LogTrace($"Got authResponse: {JsonConvert.SerializeObject(authResponse)}");

				var endPoint = RegionEndpoint.GetBySystemName(config.JobCertificate.Alias.Split(":")[3]); //Get from ARN so user does not have to enter
				_logger.LogTrace($"Got Endpoint From Job Properties JSON: {JsonConvert.SerializeObject(endPoint)}");

				Credentials credentials = Utilities.AwsAuthenticateWithWebIdentity(authResponse, endPoint, config.CertificateStoreDetails.StorePath, CustomFields.AwsRole);

				_logger.LogTrace($"Credentials JSON: {JsonConvert.SerializeObject(credentials)}");

				using (AcmClient = new AmazonCertificateManagerClient(credentials.AccessKeyId,
					credentials.SecretAccessKey, region: endPoint,
					awsSessionToken: credentials.SessionToken))
				{
					_logger.LogTrace($"AcmClient JSON: {JsonConvert.SerializeObject(AcmClient)}");
					DeleteCertificateRequest deleteRequest = new DeleteCertificateRequest(config.JobCertificate.Alias);
					_logger.LogTrace($"deleteRequest JSON: {JsonConvert.SerializeObject(deleteRequest)}");
					DeleteResponse = AsyncHelpers.RunSync(() => AcmClient.DeleteCertificateAsync(deleteRequest));
					_logger.LogTrace($"DeleteResponse JSON: {JsonConvert.SerializeObject(DeleteResponse)}");
					if (DeleteResponse.HttpStatusCode == HttpStatusCode.OK)
					{
						_logger.LogTrace($"Return Success");
						return new JobResult
						{
							Result = OrchestratorJobStatusJobResult.Success,
							JobHistoryId = config.JobHistoryId,
							FailureMessage = ""
						};
					}
					else
					{
						_logger.LogTrace($"Return Failure");
						return new JobResult
						{
							Result = OrchestratorJobStatusJobResult.Failure,
							JobHistoryId = config.JobHistoryId,
							FailureMessage =
								"Management/Remove"
						};
					}
				}
			}
			catch (Exception e)
			{
				return new JobResult
				{
					Result = OrchestratorJobStatusJobResult.Failure,
					JobHistoryId = config.JobHistoryId,
					FailureMessage =
						$"Management/Remove: {e.Message}"
				};
			}
		}

		private AuthResponse OktaAuthenticate(ManagementJobConfiguration config)
		{
			try
			{
				_logger.MethodEntry();

				var oktaAuthUrl = $"https://{config.CertificateStoreDetails.ClientMachine}/oauth2/default/v1/token";
				_logger.LogTrace($"Custom Field List: {CustomFields}");
				_logger.LogTrace($"Okta Auth URL: {oktaAuthUrl}");

				var client =
					new RestClient(oktaAuthUrl)
					{
						Timeout = -1
					};
				var request = new RestRequest(Method.POST);
				request.AddHeader("Accept", "application/json");
				var clientId = config.ServerUsername;
				var clientSecret = config.ServerPassword;
				var plainTextBytes = Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}");
				_logger.LogTrace($"Okta Auth Credentials: {plainTextBytes}");
				var authHeader = Convert.ToBase64String(plainTextBytes);
				request.AddHeader("Authorization", $"Basic {authHeader}");
				request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
				if (CustomFields != null)
				{
					request.AddParameter("grant_type", CustomFields.GrantType);
					request.AddParameter("scope", CustomFields.Scope);
				}
				var response = client.Execute(request);
				_logger.LogTrace($"Okta Auth Raw Response: {response}");
				var authResponse = JsonConvert.DeserializeObject<AuthResponse>(response.Content);
				_logger.LogTrace($"Okta Serialized Auth Response: {JsonConvert.SerializeObject(authResponse)}");

				return authResponse;
			}
			catch (Exception e)
			{
				_logger.LogError($"Error Occurred in Inventory.OktaAuthenticate: {e.Message}");
				throw;
			}
		}

		//Fetch and return the chain for a cert
		private static MemoryStream GetChain(Pkcs12Store store, string alias)
		{
			string ccs = "";

			X509CertificateEntry[] chain = store.GetCertificateChain(alias);

			foreach (X509CertificateEntry chainEntry in chain)
			{
				ccs += certStart + _pemify(Convert.ToBase64String(chainEntry.Certificate.GetEncoded())) + certEnd + "\n";
			}

			return CertStringToStream(ccs);
		}

		//Convert String To MemoryStream
		private static MemoryStream CertStringToStream(string certString)
		{
			// Builds a MemoryStream from the Base64 Encoded String Representation of a cert
			byte[] certBytes = Encoding.ASCII.GetBytes(certString);
			return new MemoryStream(certBytes);
		}
	}
}