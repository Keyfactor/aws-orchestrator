
//  Copyright 2026 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

using System;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace Keyfactor.Extensions.Orchestrator.Aws.Acm.Tests
{
    /// <summary>
    /// Builds RSA key pairs, X.509 certificates, and PKCS#12 stores entirely with BouncyCastle
    /// (no .NET CNG interop), so tests run deterministically on Windows without hitting the
    /// private-key export restrictions that affect keys imported through the .NET certificate APIs.
    /// Chains are set explicitly on the key entry so <c>Pkcs12Store.GetCertificateChain</c> returns
    /// exactly the certificates we provide, in the order we provide them (leaf first).
    /// </summary>
    internal static class BcCertFactory
    {
        private const string SignatureAlgorithm = "SHA256WithRSA";

        public static AsymmetricCipherKeyPair GenerateRsaKeyPair()
        {
            var generator = new RsaKeyPairGenerator();
            generator.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
            return generator.GenerateKeyPair();
        }

        /// <summary>
        /// Creates a certificate for <paramref name="subjectCommonName"/> signed by the holder of
        /// <paramref name="issuerPrivateKey"/>. For a self-signed cert, pass the same common name for
        /// subject and issuer and the subject's own key pair for public/private.
        /// </summary>
        public static X509Certificate CreateCertificate(
            string subjectCommonName,
            string issuerCommonName,
            AsymmetricKeyParameter subjectPublicKey,
            AsymmetricKeyParameter issuerPrivateKey)
        {
            var generator = new X509V3CertificateGenerator();
            generator.SetSerialNumber(BigInteger.ProbablePrime(120, new SecureRandom()));
            generator.SetIssuerDN(new X509Name($"CN={issuerCommonName}"));
            generator.SetSubjectDN(new X509Name($"CN={subjectCommonName}"));
            generator.SetNotBefore(DateTime.UtcNow.AddDays(-1));
            generator.SetNotAfter(DateTime.UtcNow.AddYears(1));
            generator.SetPublicKey(subjectPublicKey);

            var signatureFactory = new Asn1SignatureFactory(SignatureAlgorithm, issuerPrivateKey, new SecureRandom());
            return generator.Generate(signatureFactory);
        }

        /// <summary>Creates a self-signed certificate (issuer == subject, signed by its own key).</summary>
        public static (X509Certificate Certificate, AsymmetricCipherKeyPair KeyPair) CreateSelfSigned(string commonName)
        {
            var keyPair = GenerateRsaKeyPair();
            var certificate = CreateCertificate(commonName, commonName, keyPair.Public, keyPair.Private);
            return (certificate, keyPair);
        }

        /// <summary>
        /// Builds a PKCS#12 store containing a single key entry whose certificate chain is exactly
        /// <paramref name="chain"/>, in the order supplied (leaf first, then intermediates / root).
        /// </summary>
        public static Pkcs12Store BuildStore(string alias, AsymmetricKeyParameter privateKey, params X509Certificate[] chain)
        {
            var store = new Pkcs12StoreBuilder().Build();

            var certificateEntries = new X509CertificateEntry[chain.Length];
            for (int i = 0; i < chain.Length; i++)
            {
                certificateEntries[i] = new X509CertificateEntry(chain[i]);
            }

            store.SetKeyEntry(alias, new AsymmetricKeyEntry(privateKey), certificateEntries);
            return store;
        }
    }
}
