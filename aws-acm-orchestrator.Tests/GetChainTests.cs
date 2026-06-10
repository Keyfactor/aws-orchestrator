
//  Copyright 2026 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

using System.Collections.Generic;
using System.IO;
using System.Text;
using FluentAssertions;
using Keyfactor.Extensions.Orchestrator.Aws.Acm.Jobs;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.X509;
using Xunit;

namespace Keyfactor.Extensions.Orchestrator.Aws.Acm.Tests
{
    /// <summary>
    /// Covers <see cref="Management.GetChain"/>. The leaf/end-entity certificate is sent to ACM
    /// separately as the Certificate body of the ImportCertificateRequest, so it must NOT appear in
    /// the CertificateChain. Including it caused the leaf to show up twice within a published
    /// certificate's chain - the bug these tests guard against.
    /// </summary>
    public class GetChainTests
    {
        private const string KeyAlias = "leaf-entry";

        [Fact]
        public void GetChain_LeafAndRoot_ReturnsOnlyTheRoot_NotTheLeaf()
        {
            // Root signs the leaf directly; the PKCS#12 chain is [leaf, root].
            var (root, rootKeyPair) = BcCertFactory.CreateSelfSigned("Test Root CA");
            var leafKeyPair = BcCertFactory.GenerateRsaKeyPair();
            var leaf = BcCertFactory.CreateCertificate("Test Leaf", "Test Root CA", leafKeyPair.Public, rootKeyPair.Private);

            var store = BcCertFactory.BuildStore(KeyAlias, leafKeyPair.Private, leaf, root);

            var subjects = ReadChainSubjects(store, KeyAlias);

            subjects.Should().ContainSingle().Which.Should().Be("CN=Test Root CA");
            subjects.Should().NotContain("CN=Test Leaf");
        }

        [Fact]
        public void GetChain_LeafIntermediateAndRoot_ReturnsIntermediateAndRoot_InOrder_WithoutLeaf()
        {
            // Root -> Intermediate -> Leaf; the PKCS#12 chain is [leaf, intermediate, root].
            var (root, rootKeyPair) = BcCertFactory.CreateSelfSigned("Test Root CA");
            var intermediateKeyPair = BcCertFactory.GenerateRsaKeyPair();
            var intermediate = BcCertFactory.CreateCertificate("Test Intermediate CA", "Test Root CA", intermediateKeyPair.Public, rootKeyPair.Private);
            var leafKeyPair = BcCertFactory.GenerateRsaKeyPair();
            var leaf = BcCertFactory.CreateCertificate("Test Leaf", "Test Intermediate CA", leafKeyPair.Public, intermediateKeyPair.Private);

            var store = BcCertFactory.BuildStore(KeyAlias, leafKeyPair.Private, leaf, intermediate, root);

            var subjects = ReadChainSubjects(store, KeyAlias);

            subjects.Should().Equal("CN=Test Intermediate CA", "CN=Test Root CA");
            subjects.Should().NotContain("CN=Test Leaf");
        }

        [Fact]
        public void GetChain_LeafOnly_ReturnsNull()
        {
            // A self-signed leaf with no issuers above it: nothing to send as a chain.
            var leafKeyPair = BcCertFactory.GenerateRsaKeyPair();
            var leaf = BcCertFactory.CreateCertificate("Test Leaf", "Test Leaf", leafKeyPair.Public, leafKeyPair.Private);

            var store = BcCertFactory.BuildStore(KeyAlias, leafKeyPair.Private, leaf);

            MemoryStream chainStream = Management.GetChain(store, KeyAlias);

            chainStream.Should().BeNull("a leaf-only PFX has no intermediates, so the chain must be omitted rather than sent empty");
        }

        // Invokes the production GetChain and parses the PEM it produces back into subject DNs.
        private static List<string> ReadChainSubjects(Pkcs12Store store, string alias)
        {
            using (MemoryStream chainStream = Management.GetChain(store, alias))
            {
                chainStream.Should().NotBeNull("a chain containing intermediates should produce PEM output");

                string pem = Encoding.ASCII.GetString(chainStream.ToArray());

                var subjects = new List<string>();
                using (var reader = new StringReader(pem))
                {
                    var pemReader = new PemReader(reader);
                    object parsed;
                    while ((parsed = pemReader.ReadObject()) != null)
                    {
                        if (parsed is X509Certificate certificate)
                        {
                            subjects.Add(certificate.SubjectDN.ToString());
                        }
                    }
                }

                return subjects;
            }
        }
    }
}
