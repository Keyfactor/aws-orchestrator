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

using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using System;
using System.Collections.Generic;

namespace Keyfactor.Extensions.Orchestrator.Aws.Acm.Tests.TestUtilities
{
    /// <summary>
    /// Builds throwaway certificates and PKCS#12 stores with BouncyCastle, so the chain-splitting
    /// logic can be tested against the same object model the Management job receives at run time.
    ///
    /// Keys are deliberately 1024-bit: these certificates are never trusted, never leave the test
    /// process, and never reach AWS. The only requirement is that they parse and that the issuer
    /// links resolve, so key strength buys nothing but test latency.
    /// </summary>
    internal static class CertificateFactory
    {
        private static readonly SecureRandom Random = new SecureRandom();

        internal sealed class Issued
        {
            public X509Certificate Certificate { get; set; }
            public AsymmetricKeyParameter PrivateKey { get; set; }
        }

        internal static AsymmetricCipherKeyPair NewKeyPair()
        {
            var generator = new RsaKeyPairGenerator();
            generator.Init(new KeyGenerationParameters(Random, 1024));
            return generator.GenerateKeyPair();
        }

        /// <summary>Self-signed CA, used as the root of a test chain.</summary>
        internal static Issued SelfSignedCa(string commonName)
            => Issue(commonName, new X509Name($"CN={commonName}"), null, isCa: true);

        /// <summary>Certificate signed by <paramref name="issuer"/>.</summary>
        internal static Issued IssuedBy(string commonName, Issued issuer, bool isCa = false)
            => Issue(commonName, issuer.Certificate.SubjectDN, issuer, isCa);

        private static Issued Issue(string commonName, X509Name issuerDn, Issued issuer, bool isCa)
        {
            AsymmetricCipherKeyPair subjectKeys = NewKeyPair();
            AsymmetricKeyParameter signingKey = issuer == null ? subjectKeys.Private : issuer.PrivateKey;

            var generator = new X509V3CertificateGenerator();
            generator.SetSerialNumber(BigInteger.ProbablePrime(64, Random));
            generator.SetIssuerDN(issuerDn);
            generator.SetSubjectDN(new X509Name($"CN={commonName}"));
            generator.SetNotBefore(DateTime.UtcNow.AddDays(-1));
            generator.SetNotAfter(DateTime.UtcNow.AddDays(30));
            generator.SetPublicKey(subjectKeys.Public);
            generator.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(isCa));

            return new Issued
            {
                Certificate = generator.Generate(new Asn1SignatureFactory("SHA256WithRSA", signingKey, Random)),
                PrivateKey = subjectKeys.Private
            };
        }

        /// <summary>
        /// A PKCS#12 store holding one key entry whose chain runs leaf -> (intermediates) -> root.
        /// <paramref name="chainLength"/> counts the leaf, so 1 is a bare self-signed certificate,
        /// 2 is leaf + root, and 3 is leaf + intermediate + root.
        /// </summary>
        internal static Pkcs12Store StoreWithChain(string alias, int chainLength)
        {
            if (chainLength < 1) throw new ArgumentOutOfRangeException(nameof(chainLength));

            var chain = new List<Issued>();

            if (chainLength == 1)
            {
                // Self-signed: the leaf is its own issuer, so the chain walk terminates immediately.
                chain.Add(SelfSignedCa("self-signed.example.com"));
            }
            else
            {
                Issued root = SelfSignedCa("Test Root CA");
                Issued signer = root;
                var cas = new List<Issued> { root };

                for (int i = 0; i < chainLength - 2; i++)
                {
                    signer = IssuedBy($"Test Intermediate CA {i + 1}", signer, isCa: true);
                    cas.Add(signer);
                }

                chain.Add(IssuedBy("leaf.example.com", signer));
                // Leaf first, then issuers from nearest to furthest - the order BouncyCastle returns.
                cas.Reverse();
                chain.AddRange(cas);
            }

            var store = new Pkcs12StoreBuilder().Build();
            var entries = new X509CertificateEntry[chain.Count];
            for (int i = 0; i < chain.Count; i++)
                entries[i] = new X509CertificateEntry(chain[i].Certificate);

            store.SetKeyEntry(alias, new AsymmetricKeyEntry(chain[0].PrivateKey), entries);
            return store;
        }

        /// <summary>A store whose key entry has no certificate chain attached at all.</summary>
        internal static Pkcs12Store StoreWithNoChainForAlias(string alias)
        {
            var store = new Pkcs12StoreBuilder().Build();
            Issued leaf = SelfSignedCa("orphan.example.com");

            // Deliberately register the cert under a *different* alias so the requested alias resolves
            // to nothing and GetCertificateChain returns null.
            store.SetCertificateEntry(alias + "-other", new X509CertificateEntry(leaf.Certificate));
            return store;
        }
    }
}
