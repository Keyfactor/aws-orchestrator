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
using Keyfactor.Extensions.Orchestrator.Aws.Acm.Tests.TestUtilities;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.X509;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace Keyfactor.Extensions.Orchestrator.Aws.Acm.Tests.Jobs
{
    public class ManagementTests
    {
        private readonly ITestOutputHelper _output;

        public ManagementTests(ITestOutputHelper output) => _output = output;

        // ---------------------------------------------------------------------------------
        // Alias classification decides replace-in-place versus import-as-new.
        //
        // This replaced an alias-length heuristic that misclassified long friendly aliases, so the
        // boundary cases matter: a false positive sends a DescribeCertificate for an ARN that does
        // not exist, a false negative silently creates a duplicate certificate in ACM.
        // ---------------------------------------------------------------------------------

        [Theory]
        [InlineData("arn:aws:acm:us-east-2:905418207372:certificate/fac411f1-a478-4a8b-9680-eee1df74ca2d")]
        [InlineData("arn:aws:acm:eu-west-1:123456789012:certificate/abc")]
        [InlineData("ARN:AWS:ACM:us-east-1:1:certificate/x")]
        [InlineData("  arn:aws:acm:us-east-1:1:certificate/x  ")]
        public void AcmCertificateArns_AreRecognised(string alias)
            => Assert.True(Management.IsAcmCertificateArn(alias));

        [Theory]
        [InlineData("ARN:AWS:ACM:US-EAST-1:1:CERTIFICATE/X")]
        [InlineData("arn:aws:acm:us-east-1:1:Certificate/X")]
        [InlineData("Arn:Aws:Acm:us-east-1:1:CERTIFICATE/abc-123")]
        public void ArnMatchingIsCaseInsensitiveThroughout(string alias)
        {
            // Both halves of the check are case-insensitive. The prefix test always was; the resource
            // test was a plain ordinal Contains, so a fully uppercased ARN passed the first check and
            // failed the second. That meant a hand-entered alias would be imported as a new certificate
            // instead of replaced in place, leaving a duplicate in ACM.
            Assert.True(Management.IsAcmCertificateArn(alias));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("my-friendly-alias")]
        [InlineData("a-very-long-friendly-alias-that-the-old-length-heuristic-would-have-misread-as-an-arn")]
        public void NonArnAliases_AreNotRecognised(string alias)
            => Assert.False(Management.IsAcmCertificateArn(alias));

        [Fact]
        public void AnotherAwsServiceArn_IsNotAnAcmCertificateArn()
        {
            // Needs both the acm service prefix and the certificate resource segment.
            Assert.False(Management.IsAcmCertificateArn("arn:aws:iam::905418207372:role/iam-ACM-access"));
            Assert.False(Management.IsAcmCertificateArn("arn:aws:acm-pca:us-east-1:1:certificate-authority/x"));
        }

        [Fact]
        public void AcmArnWithoutACertificateResource_IsNotRecognised()
        {
            // An ACM ARN that is not a certificate (for example an ACME endpoint) must not be treated
            // as a certificate to replace.
            Assert.False(Management.IsAcmCertificateArn("arn:aws:acm:us-east-2:905418207372:acme-endpoint/d14a20d2"));
        }

        [Fact]
        public void GovCloudAndChinaPartitionArns_AreNotRecognised()
        {
            // Documents current behaviour rather than endorsing it: the check is hard-coded to the
            // commercial "arn:aws:acm:" prefix, so a GovCloud or China certificate would be imported as
            // new instead of replaced in place. Change this test if partition support is added.
            Assert.False(Management.IsAcmCertificateArn("arn:aws-us-gov:acm:us-gov-west-1:1:certificate/x"));
            Assert.False(Management.IsAcmCertificateArn("arn:aws-cn:acm:cn-north-1:1:certificate/x"));
        }

        // ---------------------------------------------------------------------------------
        // ACM Tags parsing - entry parameter format "Name1=Value1,Name2=Value2"
        // ---------------------------------------------------------------------------------

        private static Dictionary<string, object> Props(object value)
            => new Dictionary<string, object> { { "ACM Tags", value } };

        [Fact]
        public void NullJobProperties_YieldNoTags()
            => Assert.Empty(TestableManagement.Create(_output).ParseACMTags(null));

        [Fact]
        public void MissingAcmTagsKey_YieldsNoTags()
            => Assert.Empty(TestableManagement.Create(_output)
                .ParseACMTags(new Dictionary<string, object> { { "Something Else", "x" } }));

        [Fact]
        public void NullAcmTagsValue_YieldsNoTags()
            => Assert.Empty(TestableManagement.Create(_output).ParseACMTags(Props(null)));

        [Fact]
        public void EmptyAcmTagsValue_YieldsNoTags()
            => Assert.Empty(TestableManagement.Create(_output).ParseACMTags(Props("")));

        [Fact]
        public void SingleTag_IsParsed()
        {
            var tags = TestableManagement.Create(_output).ParseACMTags(Props("purpose=testing"));

            var tag = Assert.Single(tags);
            Assert.Equal("purpose", tag.Key);
            Assert.Equal("testing", tag.Value);
        }

        [Fact]
        public void MultipleTags_AreParsedInOrder()
        {
            var tags = TestableManagement.Create(_output).ParseACMTags(Props("Name1=Value1,Name2=Value2"));

            Assert.Equal(2, tags.Count);
            Assert.Equal(new[] { "Name1", "Name2" }, tags.Select(t => t.Key));
            Assert.Equal(new[] { "Value1", "Value2" }, tags.Select(t => t.Value));
        }

        [Fact]
        public void WhitespaceAroundTagsAndSeparators_IsTrimmed()
        {
            var tags = TestableManagement.Create(_output).ParseACMTags(Props(" Name1 = Value1 , Name2 = Value2 "));

            Assert.Equal(2, tags.Count);
            Assert.Equal("Name1", tags[0].Key);
            Assert.Equal("Value1", tags[0].Value);
            Assert.Equal("Name2", tags[1].Key);
        }

        [Fact]
        public void EmptyEntriesBetweenSeparators_AreSkipped()
        {
            var tags = TestableManagement.Create(_output).ParseACMTags(Props("a=1,,b=2,"));

            Assert.Equal(2, tags.Count);
        }

        [Fact]
        public void TagsAreRoundTrippedFromTheFormatInventoryEmits()
        {
            // Inventory writes the ACM Tags entry parameter and Management reads it back. The two
            // formats must agree or tags silently change on every replace.
            var tags = TestableManagement.Create(_output).ParseACMTags(Props("Name1=Value1,Name2=Value2"));

            string roundTripped = string.Join(",", tags.Select(t => $"{t.Key}={t.Value}"));
            Assert.Equal("Name1=Value1,Name2=Value2", roundTripped);
        }

        [Theory]
        [InlineData("justakey")]        // no separator at all
        [InlineData("a=1=2")]           // three segments
        [InlineData("=orphanvalue")]    // empty key is dropped by RemoveEmptyEntries, leaving one segment
        [InlineData("orphankey=")]      // empty value is dropped the same way
        public void MalformedTags_ThrowWithAnExplanatoryMessage(string value)
        {
            var ex = Assert.Throws<Exception>(() => TestableManagement.Create(_output).ParseACMTags(Props(value)));

            Assert.Contains("invalid format", ex.Message);
            Assert.Contains("instead of 2", ex.Message);
        }

        [Fact]
        public void NonStringAcmTagsValue_IsStringifiedBeforeParsing()
        {
            // JobProperties is Dictionary<string, object>, so the value need not already be a string.
            var tags = TestableManagement.Create(_output).ParseACMTags(Props((object)"env=prod"));

            Assert.Equal("env", Assert.Single(tags).Key);
        }

        // ---------------------------------------------------------------------------------
        // PEM line wrapping
        // ---------------------------------------------------------------------------------

        [Fact]
        public void Pemify_LeavesShortInputUntouched()
        {
            Assert.Equal("ABC", Management._pemify("ABC"));
        }

        [Fact]
        public void Pemify_WrapsAtExactly64Characters()
        {
            string body = new string('A', 64);

            Assert.Equal(body, Management._pemify(body));
        }

        [Fact]
        public void Pemify_BreaksLongInputIntoSixtyFourCharacterLines()
        {
            string body = new string('A', 150);

            string[] lines = Management._pemify(body).Split('\n');

            Assert.Equal(new[] { 64, 64, 22 }, lines.Select(l => l.Length));
            // Wrapping must be lossless - the certificate body is reassembled by the consumer.
            Assert.Equal(body, string.Concat(lines));
        }

        [Fact]
        public void Pemify_HandlesAnExactMultipleOf64WithoutATrailingBlankLine()
        {
            string body = new string('B', 128);

            string[] lines = Management._pemify(body).Split('\n');

            Assert.Equal(2, lines.Length);
            Assert.All(lines, l => Assert.Equal(64, l.Length));
        }

        // ---------------------------------------------------------------------------------
        // Certificate chain extraction from the uploaded PFX
        //
        // BouncyCastle returns the chain leaf-first. The leaf is sent separately as the Certificate
        // body of the ImportCertificateRequest, so repeating it in CertificateChain made it appear
        // twice in the published chain - the regression fixed in 3.0.x. These tests pin the leaf out
        // of the chain so it cannot come back.
        // ---------------------------------------------------------------------------------

        private const string Alias = "test-entry";
        private const string BeginMarker = "-----BEGIN CERTIFICATE-----\n";
        private const string EndMarker = "\n-----END CERTIFICATE-----";

        private static string ChainPem(Pkcs12Store store, string alias = Alias)
        {
            using MemoryStream stream = Management.GetChain(store, alias);
            return stream == null ? null : Encoding.ASCII.GetString(stream.ToArray());
        }

        private static string[] PemBlocks(string pem)
            => pem.Split(new[] { BeginMarker }, StringSplitOptions.RemoveEmptyEntries)
                  .Select(b => b.Replace(EndMarker, "").Replace("\n", "").Trim())
                  .Where(b => b.Length > 0)
                  .ToArray();

        private static string Base64Of(Org.BouncyCastle.X509.X509Certificate cert)
            => Convert.ToBase64String(cert.GetEncoded());

        [Fact]
        public void SelfSignedCertificateWithNoIntermediates_SendsNoChainAtAll()
        {
            // A chain of just the leaf must yield null, not an empty stream: ACM rejects an empty
            // CertificateChain as unparseable, so the field has to be omitted entirely.
            Assert.Null(ChainPem(CertificateFactory.StoreWithChain(Alias, chainLength: 1)));
        }

        [Fact]
        public void AliasWithNoChain_SendsNoChain()
            => Assert.Null(ChainPem(CertificateFactory.StoreWithNoChainForAlias(Alias)));

        [Fact]
        public void LeafPlusRoot_SendsOnlyTheRoot()
        {
            Pkcs12Store store = CertificateFactory.StoreWithChain(Alias, chainLength: 2);
            X509CertificateEntry[] chain = store.GetCertificateChain(Alias);

            string[] blocks = PemBlocks(ChainPem(store));

            string only = Assert.Single(blocks);
            Assert.Equal(Base64Of(chain[1].Certificate), only);
            // The explicit regression assertion: the leaf must not appear.
            Assert.DoesNotContain(Base64Of(chain[0].Certificate), only);
        }

        [Fact]
        public void LeafPlusIntermediatePlusRoot_SendsBothCasNearestFirst()
        {
            Pkcs12Store store = CertificateFactory.StoreWithChain(Alias, chainLength: 3);
            X509CertificateEntry[] chain = store.GetCertificateChain(Alias);
            Assert.Equal(3, chain.Length); // guards the fixture itself

            string[] blocks = PemBlocks(ChainPem(store));

            Assert.Equal(2, blocks.Length);
            Assert.Equal(Base64Of(chain[1].Certificate), blocks[0]);
            Assert.Equal(Base64Of(chain[2].Certificate), blocks[1]);
            Assert.DoesNotContain(Base64Of(chain[0].Certificate), string.Concat(blocks));
        }

        [Fact]
        public void ChainBlocksAreWrappedAtSixtyFourCharactersAndReParse()
        {
            string pem = ChainPem(CertificateFactory.StoreWithChain(Alias, chainLength: 3));

            Assert.Equal(2, Regex.Matches(pem, "-----BEGIN CERTIFICATE-----").Count);
            Assert.Equal(2, Regex.Matches(pem, "-----END CERTIFICATE-----").Count);

            // Every body line except the last of each block is exactly 64 characters, and the whole
            // thing is still parseable as X.509 - the real contract with ACM.
            foreach (string[] lines in pem.Split(new[] { BeginMarker }, StringSplitOptions.RemoveEmptyEntries)
                                          .Select(b => b.Replace(EndMarker, "").Trim('\n').Split('\n'))
                                          .Where(l => l.Length > 1))
            {
                Assert.All(lines.Take(lines.Length - 1), l => Assert.Equal(64, l.Length));
                Assert.InRange(lines[lines.Length - 1].Length, 1, 64);
            }

            var parser = new Org.BouncyCastle.X509.X509CertificateParser();
            Assert.Equal(2, parser.ReadCertificates(Encoding.ASCII.GetBytes(pem)).Count);
        }

        [Fact]
        public void ChainIsAsciiEncoded()
        {
            // CertStringToStream uses Encoding.ASCII. Base64 and the delimiters are all ASCII, so this
            // is lossless - but it would silently corrupt anything that was not.
            string pem = ChainPem(CertificateFactory.StoreWithChain(Alias, chainLength: 2));

            Assert.All(pem, c => Assert.True(c < 128, $"non-ASCII character U+{(int)c:X4} in chain PEM"));
        }
    }
}
