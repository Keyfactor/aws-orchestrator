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

using Amazon;
using Keyfactor.Extensions.Orchestrator.Aws.Acm.Jobs;
using Keyfactor.Extensions.Orchestrator.Aws.Acm.Tests.TestUtilities;
using System.Collections.Generic;
using System.Linq;
using Xunit.Abstractions;

namespace Keyfactor.Extensions.Orchestrator.Aws.Acm.Tests.Jobs
{
    public class DiscoveryTests
    {
        private readonly ITestOutputHelper _output;

        public DiscoveryTests(ITestOutputHelper output) => _output = output;

        // ---------------------------------------------------------------------------------
        // Per-account role ARN construction for cross-account discovery
        // ---------------------------------------------------------------------------------

        [Theory]
        [InlineData("aws", "arn:aws:iam::905418207372:role/KeyfactorACMDiscoveryRole")]
        [InlineData("aws-us-gov", "arn:aws-us-gov:iam::905418207372:role/KeyfactorACMDiscoveryRole")]
        [InlineData("aws-cn", "arn:aws-cn:iam::905418207372:role/KeyfactorACMDiscoveryRole")]
        public void MemberRoleArn_UsesThePartitionOfTheManagementAccount(string partition, string expected)
            => Assert.Equal(expected, Discovery.BuildMemberRoleArn(partition, "905418207372", "KeyfactorACMDiscoveryRole"));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("aws-iso")]
        public void UnknownPartition_FallsBackToTheCommercialPartition(string partition)
        {
            // IAM ARNs are global per partition, so an unrecognised partition name degrades to "aws"
            // rather than producing an unusable ARN.
            Assert.Equal("arn:aws:iam::1:role/R", Discovery.BuildMemberRoleArn(partition, "1", "R"));
        }

        [Fact]
        public void MemberRoleArn_HasNoRegionSegment()
        {
            // IAM is global; a region in this ARN would make every AssumeRole call fail.
            string arn = Discovery.BuildMemberRoleArn("aws", "905418207372", "R");

            Assert.Equal("arn:aws:iam::905418207372:role/R", arn);
            Assert.Contains("iam::", arn);
        }

        // ---------------------------------------------------------------------------------
        // Organizations is a single global endpoint per partition
        // ---------------------------------------------------------------------------------

        [Theory]
        [InlineData("aws", "us-east-1")]
        [InlineData("aws-us-gov", "us-gov-west-1")]
        [InlineData("aws-cn", "cn-northwest-1")]
        [InlineData("something-else", "us-east-1")]
        public void OrganizationsRegion_IsThePartitionGlobalEndpoint(string partition, string expectedRegion)
            => Assert.Equal(expectedRegion, Discovery.GetOrganizationsRegion(partition).SystemName);

        // ---------------------------------------------------------------------------------
        // Region list resolution
        // ---------------------------------------------------------------------------------

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("*")]
        [InlineData("  *  ")]
        public void BlankOrWildcardRegions_MeanEveryRegionInThePartition(string config)
        {
            // NOTE: CLAUDE.md described this as defaulting to "16 common ACM regions". It does not -
            // it returns the SDK's full region list for the partition. These assertions pin the real
            // behaviour.
            var regions = TestableDiscovery.Create(_output).ParseRegions(config, "aws");

            Assert.Contains(regions, r => r.SystemName == "us-east-1");
            Assert.Contains(regions, r => r.SystemName == "eu-west-1");
            Assert.True(regions.Count > 16, $"expected the full partition list, got {regions.Count}");
            Assert.All(regions, r => Assert.Equal("aws", r.PartitionName));
        }

        [Fact]
        public void WildcardIsScopedToTheRequestedPartition()
        {
            var commercial = TestableDiscovery.Create(_output).ParseRegions("*", "aws");

            // A commercial-partition scan must never reach into GovCloud or China.
            Assert.DoesNotContain(commercial, r => r.SystemName.StartsWith("us-gov-"));
            Assert.DoesNotContain(commercial, r => r.SystemName.StartsWith("cn-"));
        }

        [Fact]
        public void GovCloudPartition_ReturnsOnlyGovCloudRegions()
        {
            var gov = TestableDiscovery.Create(_output).ParseRegions("*", "aws-us-gov");

            Assert.NotEmpty(gov);
            Assert.All(gov, r => Assert.StartsWith("us-gov-", r.SystemName));
        }

        [Fact]
        public void ExplicitRegionList_IsUsedVerbatimAndInOrder()
        {
            var regions = TestableDiscovery.Create(_output).ParseRegions("us-east-2,eu-west-1,ap-south-1", "aws");

            Assert.Equal(new[] { "us-east-2", "eu-west-1", "ap-south-1" }, regions.Select(r => r.SystemName));
        }

        [Fact]
        public void ExplicitRegionList_TolerantOfSpacingAndTrailingSeparators()
        {
            var regions = TestableDiscovery.Create(_output).ParseRegions(" us-east-2 , , eu-west-1 ,", "aws");

            Assert.Equal(new[] { "us-east-2", "eu-west-1" }, regions.Select(r => r.SystemName));
        }

        [Fact]
        public void ExplicitRegionList_IgnoresThePartitionArgument()
        {
            // The partition only gates the wildcard path. An explicitly named region is taken at face
            // value, so an operator can pin a region the partition filter would have excluded.
            var regions = TestableDiscovery.Create(_output).ParseRegions("us-east-1", "aws-cn");

            Assert.Equal("us-east-1", Assert.Single(regions).SystemName);
        }

        // ---------------------------------------------------------------------------------
        // CSV splitting
        // ---------------------------------------------------------------------------------

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("     ")]
        public void SplitCsv_OfBlank_IsEmpty(string value)
            => Assert.Empty(Discovery.SplitCsv(value));

        [Fact]
        public void SplitCsv_TrimsAndDropsEmptyEntries()
            => Assert.Equal(new[] { "a", "b", "c" }, Discovery.SplitCsv(" a , b ,, c , "));

        [Fact]
        public void SplitCsv_OfASingleValue_YieldsOneEntry()
            => Assert.Equal(new[] { "solo" }, Discovery.SplitCsv("solo"));

        [Fact]
        public void SplitCsv_PreservesInternalSpacesWithinAnEntry()
            => Assert.Equal(new[] { "two words" }, Discovery.SplitCsv("  two words  "));

        // ---------------------------------------------------------------------------------
        // FirstNonEmpty
        // ---------------------------------------------------------------------------------

        [Theory]
        [InlineData("a", "b", "a")]
        [InlineData(null, "b", "b")]
        [InlineData("", "b", "b")]
        [InlineData("   ", "b", "b")]
        [InlineData(null, null, null)]
        public void FirstNonEmpty_PrefersTheFirstMeaningfulValue(string a, string b, string expected)
            => Assert.Equal(expected, Discovery.FirstNonEmpty(a, b));

        // ---------------------------------------------------------------------------------
        // JobProperties lookup. DiscoveryJobConfiguration has no StorePath, so every setting
        // arrives through this loosely typed dictionary.
        // ---------------------------------------------------------------------------------

        [Fact]
        public void JobProperty_OfNullDictionary_IsNull()
            => Assert.Null(TestableDiscovery.Create(_output).GetJobProperty(null, "Region"));

        [Fact]
        public void JobProperty_OfMissingKey_IsNull()
            => Assert.Null(TestableDiscovery.Create(_output)
                .GetJobProperty(new Dictionary<string, object>(), "Region"));

        [Fact]
        public void JobProperty_OfNullValue_IsNull()
            => Assert.Null(TestableDiscovery.Create(_output)
                .GetJobProperty(new Dictionary<string, object> { { "Region", null } }, "Region"));

        [Fact]
        public void JobProperty_StringifiesNonStringValues()
        {
            // Values arrive as object from Command and are not guaranteed to be strings.
            var props = new Dictionary<string, object> { { "Count", 42 }, { "Flag", true } };
            var disc = TestableDiscovery.Create(_output);

            Assert.Equal("42", disc.GetJobProperty(props, "Count"));
            Assert.Equal("True", disc.GetJobProperty(props, "Flag"));
        }

        [Fact]
        public void JobProperty_IsCaseSensitive_UnlikeJobPropertyMulti()
        {
            // Documents the difference between the two lookups: the single-key form is an ordinary
            // dictionary hit, which is why the dialog-driven fields use the Multi form instead.
            var props = new Dictionary<string, object> { { "Region", "us-east-2" } };

            Assert.Null(TestableDiscovery.Create(_output).GetJobProperty(props, "region"));
        }

        [Fact]
        public void JobPropertyMulti_MatchesKeysCaseInsensitively()
        {
            // The Discovery dialog packs its fields into JobProperties under a key whose casing varies
            // by Command version, which is the whole reason this lookup exists.
            var props = new Dictionary<string, object> { { "DIRS", "arn:aws:iam::1:role/R" } };

            Assert.Equal("arn:aws:iam::1:role/R",
                TestableDiscovery.Create(_output).GetJobPropertyMulti(props, "dirs", "DirectoriesToSearch"));
        }

        [Fact]
        public void JobPropertyMulti_TriesEachCandidateKeyInOrder()
        {
            var props = new Dictionary<string, object> { { "DirectoriesToSearch", "second-choice" } };

            Assert.Equal("second-choice",
                TestableDiscovery.Create(_output).GetJobPropertyMulti(props, "dirs", "DirectoriesToSearch"));
        }

        [Fact]
        public void JobPropertyMulti_SkipsAKeyPresentButBlank()
        {
            var props = new Dictionary<string, object>
            {
                { "dirs", "   " },
                { "DirectoriesToSearch", "real-value" }
            };

            Assert.Equal("real-value",
                TestableDiscovery.Create(_output).GetJobPropertyMulti(props, "dirs", "DirectoriesToSearch"));
        }

        [Fact]
        public void JobPropertyMulti_OfNullDictionaryOrNoMatch_IsNull()
        {
            var disc = TestableDiscovery.Create(_output);

            Assert.Null(disc.GetJobPropertyMulti(null, "dirs"));
            Assert.Null(disc.GetJobPropertyMulti(new Dictionary<string, object>(), "dirs"));
        }

        // ---------------------------------------------------------------------------------
        // Discovered store paths
        // ---------------------------------------------------------------------------------

        [Fact]
        public void DiscoveredStorePath_IsSelfContainedAndReParsable()
        {
            // Discovery has no channel to set ClientMachine per discovered store, so the path has to
            // carry both halves of the store identity. Inventory and Management must be able to read
            // back exactly what Discovery emitted.
            string roleArn = "arn:aws:iam::905418207372:role/KeyfactorACMDiscoveryRole";
            string path = StorePathParser.Build(roleArn, "us-east-2");

            var (parsedRole, parsedRegion) = StorePathParser.Parse(path, clientMachine: "UOLinux");

            Assert.Equal(roleArn, parsedRole);
            Assert.Equal("us-east-2", parsedRegion);
        }

        [Fact]
        public void ExtensionName_MatchesTheRegisteredStoreType()
        {
            // Must equal the store type registered in Command and the class names in manifest.json.
            Assert.Equal("AWS-ACM-v3", TestableDiscovery.Create(_output).ExtensionName);
        }
    }
}
