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


using Keyfactor.Extensions.Aws.Models;
using Keyfactor.Extensions.Orchestrator.Aws.Acm.Jobs;

namespace Keyfactor.Extensions.Orchestrator.Aws.Acm.Tests.Jobs
{
    /// <summary>
    /// Every job logs which of the four supported credential methods it took, in the auth library's
    /// vocabulary. The strings are a deliberate, greppable contract (operators and the test harnesses
    /// match on them), so they are asserted literally.
    /// </summary>
    public class AuthDescriptionTests
    {
        private const string RoleArn = "arn:aws:iam::905418207372:role/iam-ACM-access";
        private const string ProfileRef = "[kfprofile]arn:aws:iam::905418207372:role/iam-ACM-access";

        [Fact]
        public void NullCustomFields_IsUnknown()
            => Assert.Equal("Unknown", AuthDescription.DescribeMethod(null, RoleArn));

        [Fact]
        public void NoMethodSelected_IsUnknown()
            => Assert.Equal("Unknown", AuthDescription.DescribeMethod(new AuthCustomFieldParameters(), RoleArn));

        [Fact]
        public void OAuth_WinsOverEverythingElse()
        {
            // Precedence is OAuth > IAM > DefaultSdk. Asserted with all three set so a reordering of the
            // branches is caught rather than passing by luck.
            var cf = new AuthCustomFieldParameters
            {
                UseOAuth = true,
                UseIAM = true,
                UseDefaultSdkAuth = true,
                DefaultSdkAssumeRole = true
            };

            Assert.Equal("OAuthProvider", AuthDescription.DescribeMethod(cf, RoleArn));
        }

        [Fact]
        public void Iam_WinsOverDefaultSdk()
        {
            var cf = new AuthCustomFieldParameters
            {
                UseIAM = true,
                UseDefaultSdkAuth = true,
                DefaultSdkAssumeRole = true
            };

            Assert.Equal("IamUser", AuthDescription.DescribeMethod(cf, RoleArn));
        }

        [Theory]
        [InlineData(RoleArn, false, "DefaultSdk")]
        [InlineData(RoleArn, true, "DefaultSdk_AssumeRole")]
        [InlineData(ProfileRef, false, "DefaultSdk_CredentialProfile")]
        [InlineData(ProfileRef, true, "DefaultSdk_CredentialProfile_AssumeRole")]
        [InlineData(null, false, "DefaultSdk")]
        [InlineData("", true, "DefaultSdk_AssumeRole")]
        public void DefaultSdk_DistinguishesProfileAndAssumeRole(string roleArn, bool assumeRole, string expected)
        {
            var cf = new AuthCustomFieldParameters
            {
                UseDefaultSdkAuth = true,
                DefaultSdkAssumeRole = assumeRole
            };

            Assert.Equal(expected, AuthDescription.DescribeMethod(cf, roleArn));
        }

        [Fact]
        public void CredentialProfile_IsDetectedFromLeadingBracketAfterTrimming()
        {
            // The auth library's marker is a "[profile]" prefix; leading whitespace must not hide it.
            var cf = new AuthCustomFieldParameters { UseDefaultSdkAuth = true };

            Assert.Equal("DefaultSdk_CredentialProfile", AuthDescription.DescribeMethod(cf, "   [kfprofile]whatever"));
        }

        [Fact]
        public void BracketLaterInTheString_IsNotACredentialProfile()
        {
            var cf = new AuthCustomFieldParameters { UseDefaultSdkAuth = true };

            Assert.Equal("DefaultSdk", AuthDescription.DescribeMethod(cf, "arn:aws:iam::1:role/weird[name]"));
        }
    }
}
