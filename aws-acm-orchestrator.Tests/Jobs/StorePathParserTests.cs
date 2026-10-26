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


using Keyfactor.Extensions.Orchestrator.Aws.Acm.Jobs;

namespace Keyfactor.Extensions.Orchestrator.Aws.Acm.Tests.Jobs
{
    /// <summary>
    /// A store's identity is Role ARN + Region. Legacy stores carry the region in StorePath and the ARN
    /// in ClientMachine; stores emitted by cross-account Discovery fold both into "&lt;roleArn&gt;|&lt;region&gt;".
    /// Both shapes must resolve through one code path (v3.1.0).
    /// </summary>
    public class StorePathParserTests
    {
        private const string RoleArn = "arn:aws:iam::905418207372:role/iam-ACM-access";

        [Fact]
        public void CombinedPath_TakesBothValuesFromStorePath()
        {
            var (role, region) = StorePathParser.Parse($"{RoleArn}|us-east-2", "ignored-client-machine");

            Assert.Equal(RoleArn, role);
            Assert.Equal("us-east-2", region);
        }

        [Fact]
        public void LegacyPath_TakesRegionFromStorePathAndRoleFromClientMachine()
        {
            var (role, region) = StorePathParser.Parse("us-east-1", RoleArn);

            Assert.Equal(RoleArn, role);
            Assert.Equal("us-east-1", region);
        }

        [Fact]
        public void CombinedPath_TrimsWhitespaceAroundBothSegments()
        {
            var (role, region) = StorePathParser.Parse($"  {RoleArn}  |  us-west-2  ", null);

            Assert.Equal(RoleArn, role);
            Assert.Equal("us-west-2", region);
        }

        [Fact]
        public void CombinedPath_WithEmptyRoleSegment_FallsBackToClientMachine()
        {
            // Documented behaviour: a hand-edited "|us-east-1" still works.
            var (role, region) = StorePathParser.Parse("|us-east-1", RoleArn);

            Assert.Equal(RoleArn, role);
            Assert.Equal("us-east-1", region);
        }

        [Fact]
        public void CombinedPath_SplitsOnlyOnTheFirstSeparator()
        {
            // Split is bounded to 2 parts, so a stray separator stays inside the region segment rather
            // than silently truncating it.
            var (role, region) = StorePathParser.Parse($"{RoleArn}|us-east-2|extra", null);

            Assert.Equal(RoleArn, role);
            Assert.Equal("us-east-2|extra", region);
        }

        [Fact]
        public void CombinedPath_WithNoRegionSegment_YieldsEmptyRegion()
        {
            var (role, region) = StorePathParser.Parse($"{RoleArn}|", "cm");

            Assert.Equal(RoleArn, role);
            Assert.Equal(string.Empty, region);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void BlankStorePath_FallsThroughToLegacyShape(string storePath)
        {
            var (role, region) = StorePathParser.Parse(storePath, RoleArn);

            Assert.Equal(RoleArn, role);
            // Legacy shape trims, and a null StorePath stays null rather than becoming "".
            Assert.Equal(storePath?.Trim(), region);
        }

        [Fact]
        public void Build_ProducesAPathThatParseRoundTrips()
        {
            string built = StorePathParser.Build(RoleArn, "eu-west-1");

            Assert.Equal($"{RoleArn}|eu-west-1", built);

            var (role, region) = StorePathParser.Parse(built, null);
            Assert.Equal(RoleArn, role);
            Assert.Equal("eu-west-1", region);
        }

        [Fact]
        public void Separator_IsAbsentFromArnsAndRegionNames()
        {
            // The separator choice is what makes the combined path unambiguous. If '|' ever became
            // legal in an ARN or region name this assumption would silently break.
            Assert.DoesNotContain(StorePathParser.Separator.ToString(), RoleArn);
            Assert.DoesNotContain(StorePathParser.Separator.ToString(), "us-east-2");
        }
    }
}
