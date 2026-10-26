// Copyright 2025 Keyfactor
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


using Amazon.CertificateManager;
using Amazon.CertificateManager.Model;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Keyfactor.Extensions.Orchestrator.Aws.Acm.Tests.TestUtilities
{
    /// <summary>
    /// Builders for a fake <see cref="IAmazonCertificateManager"/>. The job classes hold their client
    /// in an internal field of the interface type, so a test can substitute one and exercise the real
    /// job logic without reaching AWS.
    /// </summary>
    internal static class AcmMocks
    {
        /// <summary>A syntactically valid PEM whose body is the Base64 of "ABC".</summary>
        public const string SamplePem = "-----BEGIN CERTIFICATE-----\nQUJD\n-----END CERTIFICATE-----\n";

        public const string SampleBase64 = "QUJD";

        public static Mock<IAmazonCertificateManager> Client(
            string certificatePem = SamplePem,
            IEnumerable<KeyValuePair<string, string>> tags = null)
        {
            var mock = new Mock<IAmazonCertificateManager>(MockBehavior.Loose);

            mock.Setup(c => c.GetCertificateAsync(It.IsAny<GetCertificateRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetCertificateResponse { Certificate = certificatePem });

            var tagList = (tags ?? Enumerable.Empty<KeyValuePair<string, string>>())
                .Select(kv => new Amazon.CertificateManager.Model.Tag { Key = kv.Key, Value = kv.Value })
                .ToList();

            mock.Setup(c => c.ListTagsForCertificateAsync(It.IsAny<ListTagsForCertificateRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ListTagsForCertificateResponse { Tags = tagList });

            return mock;
        }

        /// <summary>
        /// Returns the supplied pages from ListCertificates in order, wiring NextToken so the job's
        /// pagination loop terminates after the final page.
        /// </summary>
        public static Mock<IAmazonCertificateManager> PagingClient(params List<CertificateSummary>[] pages)
        {
            var mock = Client();
            var queue = new Queue<ListCertificatesResponse>();
            for (int i = 0; i < pages.Length; i++)
            {
                queue.Enqueue(new ListCertificatesResponse
                {
                    CertificateSummaryList = pages[i],
                    NextToken = i == pages.Length - 1 ? null : $"token-{i}"
                });
            }

            mock.Setup(c => c.ListCertificatesAsync(It.IsAny<ListCertificatesRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => queue.Count > 0
                    ? queue.Dequeue()
                    : new ListCertificatesResponse { CertificateSummaryList = new List<CertificateSummary>(), NextToken = null });

            return mock;
        }

        public static CertificateSummary Summary(string arn, CertificateType type = null,
            CertificateKeyPairOrigin origin = null, string domain = "example.com")
        {
            return new CertificateSummary
            {
                CertificateArn = arn,
                DomainName = domain,
                Type = type,
                CertificateKeyPairOrigin = origin
            };
        }
    }
}
