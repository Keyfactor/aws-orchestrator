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
using Keyfactor.Extensions.Orchestrator.Aws.Acm.Jobs;
using Keyfactor.Extensions.Orchestrator.Aws.Acm.Tests.TestUtilities;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit.Abstractions;

namespace Keyfactor.Extensions.Orchestrator.Aws.Acm.Tests.Jobs
{
    public class InventoryTests
    {
        private readonly ITestOutputHelper _output;

        public InventoryTests(ITestOutputHelper output) => _output = output;

        private const string Arn = "arn:aws:acm:us-east-2:905418207372:certificate/fac411f1-a478-4a8b-9680-eee1df74ca2d";

        private static KeyValuePair<string, string>[] Tags(params (string K, string V)[] pairs)
            => pairs.Select(p => new KeyValuePair<string, string>(p.K, p.V)).ToArray();

        // ---------------------------------------------------------------------------------
        // PrivateKeyEntry is derived from the certificate Type (v3.1.1).
        //
        // ACM holds a private key only for user-imported certificates. Reporting the ones it
        // issues itself as private key entries made Command offer a renewal that Management is
        // guaranteed to reject, so the flag must follow Type and nothing else.
        // ---------------------------------------------------------------------------------

        [Fact]
        public void ImportedCertificate_IsAPrivateKeyEntry()
        {
            var inv = new TestableInventory(AcmMocks.Client().Object, _output);

            var item = inv.Build(Arn, CertificateType.IMPORTED);

            Assert.True(item.PrivateKeyEntry);
        }

        [Theory]
        [InlineData("AMAZON_ISSUED")] // includes ACME-issued certificates
        [InlineData("PRIVATE")]       // AWS Private CA
        public void CertificatesAcmIssuedItself_AreNotPrivateKeyEntries(string type)
        {
            var inv = new TestableInventory(AcmMocks.Client().Object, _output);

            var item = inv.Build(Arn, CertificateType.FindValue(type));

            Assert.False(item.PrivateKeyEntry);
        }

        [Fact]
        public void AbsentType_IsTreatedAsNotImported()
        {
            // ConstantClass equality is null-safe and yields false, so an absent Type suppresses the
            // renewal rather than offering one that always fails.
            var inv = new TestableInventory(AcmMocks.Client().Object, _output);

            var item = inv.Build(Arn, null);

            Assert.False(item.PrivateKeyEntry);
        }

        [Fact]
        public void UnrecognisedFutureType_IsTreatedAsNotImported()
        {
            // CertificateType is a ConstantClass, so AWS can introduce a value this build has never
            // heard of. It must not be mistaken for IMPORTED.
            var inv = new TestableInventory(AcmMocks.Client().Object, _output);

            var item = inv.Build(Arn, CertificateType.FindValue("SOME_FUTURE_TYPE"));

            Assert.False(item.PrivateKeyEntry);
        }

        // ---------------------------------------------------------------------------------
        // Item shape
        // ---------------------------------------------------------------------------------

        [Fact]
        public void Alias_IsTheFullAcmArn()
        {
            // Management parses the alias back as an ARN to decide replace-vs-new, so the alias must
            // stay the unmodified ARN.
            var inv = new TestableInventory(AcmMocks.Client().Object, _output);

            var item = inv.Build(Arn, CertificateType.IMPORTED);

            Assert.Equal(Arn, item.Alias);
            Assert.True(Management.IsAcmCertificateArn(item.Alias));
        }

        [Fact]
        public void ItemStatus_IsUnknownSoCommandComputesTheDiff()
        {
            var inv = new TestableInventory(AcmMocks.Client().Object, _output);

            var item = inv.Build(Arn, CertificateType.IMPORTED);

            Assert.Equal(Keyfactor.Orchestrators.Common.Enums.OrchestratorInventoryItemStatus.Unknown, item.ItemStatus);
            Assert.False(item.UseChainLevel);
        }

        [Fact]
        public void Certificate_IsSentAsBareBase64WithPemAnchorsRemoved()
        {
            var inv = new TestableInventory(AcmMocks.Client().Object, _output);

            var item = inv.Build(Arn, CertificateType.IMPORTED);

            string body = Assert.Single(item.Certificates);
            Assert.Equal(AcmMocks.SampleBase64, body);
            Assert.DoesNotContain("BEGIN CERTIFICATE", body);
            Assert.DoesNotContain("END CERTIFICATE", body);
        }

        [Fact]
        public void CarriageReturnsAreStrippedSoCrlfPemIsHandled()
        {
            var crlf = "-----BEGIN CERTIFICATE-----\r\nQUJD\r\n-----END CERTIFICATE-----\r\n";
            var inv = new TestableInventory(AcmMocks.Client(crlf).Object, _output);

            var item = inv.Build(Arn, CertificateType.IMPORTED);

            Assert.Equal(AcmMocks.SampleBase64, Assert.Single(item.Certificates));
        }

        [Fact]
        public void InteriorLineBreaksInTheBase64BodyArePreserved()
        {
            // Real PEM bodies wrap at 64 characters. Only the leading/trailing whitespace is trimmed -
            // interior wrapping is left in place, which Command tolerates because Base64 decoding skips
            // whitespace. Keeping it means the emitted value is unchanged from previous releases.
            var wrapped = "-----BEGIN CERTIFICATE-----\nQUJD\nREVG\n-----END CERTIFICATE-----\n";
            var inv = new TestableInventory(AcmMocks.Client(wrapped).Object, _output);

            var item = inv.Build(Arn, CertificateType.IMPORTED);

            Assert.Equal("QUJD\nREVG", Assert.Single(item.Certificates));
        }

        [Theory]
        [InlineData("-----BEGIN CERTIFICATE-----\nQUJD\n-----END CERTIFICATE-----")]       // no trailing newline
        [InlineData("-----BEGIN CERTIFICATE-----\nQUJD\n-----END CERTIFICATE-----\n\n")] // extra trailing newline
        [InlineData("-----BEGIN CERTIFICATE-----QUJD-----END CERTIFICATE-----")]           // no newlines at all
        [InlineData("\n-----BEGIN CERTIFICATE-----\nQUJD\n-----END CERTIFICATE-----\n")]  // leading newline
        public void AnchorsAreStrippedRegardlessOfSurroundingNewlines(string pem)
        {
            // The stripping used to fold the newlines into the delimiter match, so the footer survived
            // unless a newline followed it - shipping literal "-----END CERTIFICATE-----" to Command
            // inside the Base64 body. ACM always sends that newline, so this never fired in production,
            // but nothing guaranteed it would keep doing so.
            var inv = new TestableInventory(AcmMocks.Client(pem).Object, _output);

            var item = inv.Build(Arn, CertificateType.IMPORTED);

            Assert.Equal(AcmMocks.SampleBase64, Assert.Single(item.Certificates));
        }

        [Fact]
        public void ARealCertificateRoundTripsThroughTheInventoryPath()
        {
            // The contract that actually matters: whatever Inventory puts in Certificates[] must decode
            // back to the DER that ACM returned. Uses a genuine certificate with a full-width,
            // 64-character-wrapped Base64 body rather than the short "QUJD" fixture.
            var cert = CertificateFactory.SelfSignedCa("roundtrip.example.com").Certificate;
            string pem = "-----BEGIN CERTIFICATE-----\n"
                       + Management._pemify(Convert.ToBase64String(cert.GetEncoded()))
                       + "\n-----END CERTIFICATE-----\n";
            var inv = new TestableInventory(AcmMocks.Client(pem).Object, _output);

            var item = inv.Build(Arn, CertificateType.IMPORTED);

            string body = Assert.Single(item.Certificates);
            Assert.DoesNotContain("CERTIFICATE-----", body);
            Assert.Equal(cert.GetEncoded(), Convert.FromBase64String(body));
        }

        // ---------------------------------------------------------------------------------
        // ACM Tags entry parameter
        // ---------------------------------------------------------------------------------

        [Fact]
        public void NoTags_YieldsAnEmptyAcmTagsParameter()
        {
            var inv = new TestableInventory(AcmMocks.Client().Object, _output);

            var item = inv.Build(Arn, CertificateType.IMPORTED);

            Assert.Equal(string.Empty, item.Parameters["ACM Tags"]);
        }

        [Fact]
        public void SingleTag_HasNoLeadingSeparator()
        {
            var inv = new TestableInventory(AcmMocks.Client(tags: Tags(("purpose", "test"))).Object, _output);

            var item = inv.Build(Arn, CertificateType.IMPORTED);

            Assert.Equal("purpose=test", item.Parameters["ACM Tags"]);
        }

        [Fact]
        public void MultipleTags_AreCommaSeparatedInAcmOrder()
        {
            var inv = new TestableInventory(
                AcmMocks.Client(tags: Tags(("Name1", "Value1"), ("Name2", "Value2"))).Object, _output);

            var item = inv.Build(Arn, CertificateType.IMPORTED);

            // Round-trips through the exact format Management.ParseACMTags consumes.
            Assert.Equal("Name1=Value1,Name2=Value2", item.Parameters["ACM Tags"]);
        }

        [Fact]
        public void TagWithEmptyValue_StillEmitsTheEqualsSign()
        {
            var inv = new TestableInventory(AcmMocks.Client(tags: Tags(("flag", ""))).Object, _output);

            var item = inv.Build(Arn, CertificateType.IMPORTED);

            Assert.Equal("flag=", item.Parameters["ACM Tags"]);
        }

        // ---------------------------------------------------------------------------------
        // Failure handling
        // ---------------------------------------------------------------------------------

        [Fact]
        public void GetCertificateFailure_PropagatesSoTheBatchCanSkipTheItem()
        {
            // PerformInventory catches this per certificate, logs the ARN, raises the warning flag and
            // carries on. The contract of BuildInventoryItem is to rethrow, not to swallow.
            var mock = AcmMocks.Client();
            mock.Setup(c => c.GetCertificateAsync(It.IsAny<GetCertificateRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ResourceNotFoundException("certificate is gone"));

            var inv = new TestableInventory(mock.Object, _output);

            var ex = Assert.ThrowsAny<Exception>(() => inv.Build(Arn, CertificateType.IMPORTED));
            Assert.Contains("gone", ex.Message);
        }

        [Fact]
        public void ListTagsFailure_AlsoPropagates()
        {
            var mock = AcmMocks.Client();
            mock.Setup(c => c.ListTagsForCertificateAsync(It.IsAny<ListTagsForCertificateRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidArnException("bad arn"));

            var inv = new TestableInventory(mock.Object, _output);

            Assert.ThrowsAny<Exception>(() => inv.Build(Arn, CertificateType.IMPORTED));
        }

        [Fact]
        public void ExactlyTwoApiCallsAreMadePerCertificate()
        {
            // The cost model is two calls per certificate on top of the paged ListCertificates, and
            // deriving PrivateKeyEntry must not have added a DescribeCertificate. A regression here
            // multiplies the AWS call volume of every inventory job.
            var mock = AcmMocks.Client();
            var inv = new TestableInventory(mock.Object, _output);

            inv.Build(Arn, CertificateType.IMPORTED);

            mock.Verify(c => c.GetCertificateAsync(It.IsAny<GetCertificateRequest>(), It.IsAny<CancellationToken>()), Times.Once);
            mock.Verify(c => c.ListTagsForCertificateAsync(It.IsAny<ListTagsForCertificateRequest>(), It.IsAny<CancellationToken>()), Times.Once);
            mock.Verify(c => c.DescribeCertificateAsync(It.IsAny<DescribeCertificateRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public void TheArnIsPassedThroughToBothApiCalls()
        {
            var mock = AcmMocks.Client();
            var inv = new TestableInventory(mock.Object, _output);

            inv.Build(Arn, CertificateType.IMPORTED);

            mock.Verify(c => c.GetCertificateAsync(
                It.Is<GetCertificateRequest>(r => r.CertificateArn == Arn), It.IsAny<CancellationToken>()), Times.Once);
            mock.Verify(c => c.ListTagsForCertificateAsync(
                It.Is<ListTagsForCertificateRequest>(r => r.CertificateArn == Arn), It.IsAny<CancellationToken>()), Times.Once);
        }

        // ---------------------------------------------------------------------------------
        // StoreRef, the operator-facing store identifier embedded in every job message
        // ---------------------------------------------------------------------------------

        [Fact]
        public void StoreRef_NamesBothHalvesOfTheStoreIdentity()
        {
            string reference = Inventory.StoreRef("acmtest-defaultsdk-assume", "arn:aws:iam::1:role/r|us-east-2");

            Assert.Equal(
                "store [ClientMachine='acmtest-defaultsdk-assume', StorePath='arn:aws:iam::1:role/r|us-east-2']",
                reference);
        }

        [Fact]
        public void StoreRef_RendersNullsWithoutThrowing()
        {
            // Failure messages must survive a half-configured store.
            string reference = Inventory.StoreRef(null, null);

            Assert.Equal("store [ClientMachine='', StorePath='']", reference);
        }
    }
}
