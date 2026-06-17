
//  Copyright 2026 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

using FluentAssertions;
using Keyfactor.Extensions.Orchestrator.Aws.Acm.Jobs;
using Xunit;

namespace Keyfactor.Extensions.Orchestrator.Aws.Acm.Tests
{
    /// <summary>
    /// Covers <see cref="Management.IsAcmCertificateArn"/>, which decides whether an Add job is a
    /// replace/renewal of an existing ACM certificate (alias is an ACM ARN) or a brand-new import.
    /// This replaced a brittle "alias length &gt;= 20" heuristic that could misclassify a long
    /// friendly alias as an ARN.
    /// </summary>
    public class IsAcmCertificateArnTests
    {
        [Theory]
        [InlineData("arn:aws:acm:us-east-1:123456789012:certificate/12345678-1234-1234-1234-123456789012")]
        [InlineData("arn:aws:acm:eu-west-2:000000000000:certificate/abcdef")]
        [InlineData("  arn:aws:acm:us-east-1:123456789012:certificate/abc  ")] // surrounding whitespace is tolerated
        [InlineData("ARN:AWS:ACM:us-east-1:123456789012:certificate/abc")]     // prefix match is case-insensitive
        public void IsAcmCertificateArn_AcmCertificateArns_ReturnTrue(string alias)
        {
            Management.IsAcmCertificateArn(alias).Should().BeTrue();
        }

        [Theory]
        [InlineData("prod-web-2025")]
        [InlineData("my-friendly-cert-alias-2025")] // 27 chars: WOULD have passed the old "length >= 20" heuristic
        [InlineData("")]
        [InlineData(null)]
        [InlineData("   ")]
        [InlineData("arn:aws:iam::123456789012:role/MyRole")]                       // an ARN, but not for ACM
        [InlineData("arn:aws:acm:us-east-1:123456789012:certificate-authority/abc")] // ACM PCA-style, not a certificate
        public void IsAcmCertificateArn_NonAcmCertificateAliases_ReturnFalse(string alias)
        {
            Management.IsAcmCertificateArn(alias).Should().BeFalse();
        }
    }
}
