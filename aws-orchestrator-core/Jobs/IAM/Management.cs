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
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Amazon;
using Amazon.CertificateManager;
using Amazon.CertificateManager.Model;
using Amazon.Runtime.Internal.Util;
using Amazon.SecurityToken.Model;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;

namespace Keyfactor.AnyAgent.AwsCertificateManager.Jobs.IAM
{
	public class Management : IManagementJobExtension
	{
		public string ExtensionName => "AWSCerManA";

		private static String certStart = "-----BEGIN CERTIFICATE-----\n";
		private static String certEnd = "\n-----END CERTIFICATE-----";

		// ReSharper disable once FieldCanBeMadeReadOnly.Local
		private static Func<String, String> _pemify = (ss => ss.Length <= 64 ? ss : ss.Substring(0, 64) + "\n" + _pemify(ss.Substring(64)));

		protected internal virtual IAmazonCertificateManager AcmClient { get; set; }
		protected internal virtual DescribeCertificateResponse DescribeCertificateResponse { get; set; }
		protected internal virtual ImportCertificateResponse IcrResponse { get; set; }
		protected internal virtual DeleteCertificateResponse DeleteResponse { get; set; }
		protected internal virtual AsymmetricKeyEntry KeyEntry { get; set; }
		protected internal virtual IAMCustomFields CustomFields { get; set; }

		private readonly ILogger<Management> _logger;

		public Management(ILogger<Management> logger) =>
			_logger = logger;

		public JobResult ProcessJob(ManagementJobConfiguration jobConfiguration)
		{
			CustomFields = JsonConvert.DeserializeObject<IAMCustomFields>(jobConfiguration.CertificateStoreDetails.Properties,
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

				var endPoint = RegionEndpoint.GetBySystemName(config.JobProperties["AWS Region"].ToString());
				_logger.LogTrace($"Got Endpoint From Job Properties JSON: {JsonConvert.SerializeObject(endPoint)}");

				//Credentials credentials = Utilities.AwsAuthenticate(config.ServerUsername, config.ServerPassword, config.CertificateStoreDetails.StorePath, CustomFields.AwsRole);
				Credentials credentials = Utilities.DefaultAuthenticate(config.CertificateStoreDetails.StorePath, CustomFields.AwsRole);

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

				var endPoint = RegionEndpoint.GetBySystemName(config.JobCertificate.Alias.Split(":")[3]); //Get from ARN so user does not have to enter
				_logger.LogTrace($"Got Endpoint From Job Properties JSON: {JsonConvert.SerializeObject(endPoint)}");

				//Credentials credentials = Utilities.AwsAuthenticate(config.ServerUsername, config.ServerPassword, config.CertificateStoreDetails.StorePath, CustomFields.AwsRole);
				Credentials credentials = Utilities.DefaultAuthenticate(config.CertificateStoreDetails.StorePath, CustomFields.AwsRole);

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