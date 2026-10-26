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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Keyfactor.Extensions.Orchestrator.Aws.Acm.Tests.Jobs
{
    /// <summary>
    /// Guards the ListCertificates filter lists.
    ///
    /// Two facts make this necessary. First, ACM's filters are *inclusion* filters: anything not
    /// named is silently absent from the result with no error, which is how ACME-origin certificates
    /// and RSA-3072 certificates went missing in earlier releases. Second,
    /// <see cref="CertificateKeyPairOrigin"/> and <see cref="KeyAlgorithm"/> are AWS ConstantClass
    /// types rather than C# enums, so production code cannot enumerate them at run time - the lists
    /// are maintained by hand and a new AWS value is invisible until someone notices missing
    /// certificates in Command.
    ///
    /// Tests cannot call PerformInventory directly (it reassigns AcmClient to a real
    /// AmazonCertificateManagerClient before touching the filters), so these assertions read the
    /// source files instead. That is unusual, and deliberate: it is the only non-invasive way to pin
    /// the invariant. If a CreateAcmClient seam is ever added to Inventory, replace these with real
    /// behavioural tests against a mocked client.
    /// </summary>
    public class FilterParityTests
    {
        private static string RepoRoot([CallerFilePath] string thisFile = null)
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile), "..", ".."));

        private static string ReadJobSource(string fileName)
        {
            string path = Path.Combine(RepoRoot(), "aws-acm-orchestrator", "Jobs", fileName);
            Assert.True(File.Exists(path), $"expected to find job source at {path}");
            return File.ReadAllText(path);
        }

        /// <summary>Every <c>Prefix.VALUE</c> reference in the source, as a set of VALUE names.</summary>
        private static HashSet<string> ReferencedConstants(string source, string typeName)
            => new HashSet<string>(
                Regex.Matches(source, $@"(?<!//[^\n]*){Regex.Escape(typeName)}\.(?<name>[A-Za-z0-9_]+)")
                     .Cast<Match>()
                     .Select(m => m.Groups["name"].Value),
                StringComparer.Ordinal);

        /// <summary>Every constant the AWS SDK declares on a ConstantClass type.</summary>
        private static HashSet<string> SdkConstants<T>()
            => new HashSet<string>(
                typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static)
                         .Where(f => f.FieldType == typeof(T))
                         .Select(f => f.Name),
                StringComparer.Ordinal);

        [Fact]
        public void InventoryRequestsEveryKeyPairOriginTheSdkDefines()
        {
            var expected = SdkConstants<CertificateKeyPairOrigin>();
            var referenced = ReferencedConstants(ReadJobSource("Inventory.cs"), "CertificateKeyPairOrigin");

            Assert.NotEmpty(expected);
            AssertCovers(expected, referenced, "Inventory.cs", "CertificateKeyPairOrigin");
        }

        [Fact]
        public void DiscoveryRequestsEveryKeyPairOriginTheSdkDefines()
        {
            var expected = SdkConstants<CertificateKeyPairOrigin>();
            var referenced = ReferencedConstants(ReadJobSource("Discovery.cs"), "CertificateKeyPairOrigin");

            Assert.NotEmpty(expected);
            AssertCovers(expected, referenced, "Discovery.cs", "CertificateKeyPairOrigin");
        }

        [Fact]
        public void InventoryAndDiscoveryRequestTheSameKeyPairOrigins()
        {
            // The Discovery probe decides whether a region gets a certificate store at all. If it
            // filters more narrowly than Inventory, a region full of certificates looks empty and no
            // store is ever created for it - the 3.1.1 ACME bug.
            var inventory = ReferencedConstants(ReadJobSource("Inventory.cs"), "CertificateKeyPairOrigin");
            var discovery = ReferencedConstants(ReadJobSource("Discovery.cs"), "CertificateKeyPairOrigin");

            Assert.Equal(inventory.OrderBy(s => s), discovery.OrderBy(s => s));
        }

        [Theory]
        [InlineData("Inventory.cs")]
        [InlineData("Discovery.cs")]
        public void BothJobsRequestEveryKeyAlgorithmTheSdkDefines(string fileName)
        {
            // Includes.KeyTypes works around ACM omitting certificates that have no SAN. Leaving an
            // algorithm out hides every SAN-less certificate using it - this is how RSA_3072
            // certificates were missing before 3.1.1.
            var expected = SdkConstants<KeyAlgorithm>();
            var referenced = ReferencedConstants(ReadJobSource(fileName), "KeyAlgorithm");

            Assert.NotEmpty(expected);
            AssertCovers(expected, referenced, fileName, "KeyAlgorithm");
        }

        [Fact]
        public void InventoryAndDiscoveryRequestTheSameKeyAlgorithms()
        {
            // Same hazard as the origin lists, and the one this guard originally missed: the Discovery
            // probe applied no KeyTypes filter at all, so a region whose certificates all lacked a SAN
            // looked empty and no store was created for it.
            var inventory = ReferencedConstants(ReadJobSource("Inventory.cs"), "KeyAlgorithm");
            var discovery = ReferencedConstants(ReadJobSource("Discovery.cs"), "KeyAlgorithm");

            Assert.Equal(inventory.OrderBy(s => s), discovery.OrderBy(s => s));
        }

        private static void AssertCovers(HashSet<string> expected, HashSet<string> referenced,
            string fileName, string typeName)
        {
            var missing = expected.Except(referenced).OrderBy(s => s).ToList();

            Assert.True(missing.Count == 0,
                $"{fileName} does not request {typeName}.{{{string.Join(", ", missing)}}}. " +
                $"ACM filters are inclusion filters, so certificates matching the missing " +
                $"value(s) will be silently absent from the result. Add them to the filter list " +
                $"(and to the matching list in the other job) rather than relaxing this test.");
        }

        [Fact]
        public void TheSdkConstantsAreNotEnumerableAtRunTime_WhichIsWhyTheseTestsExist()
        {
            // Documents the constraint these tests work around: ConstantClass is not an enum, so
            // Enum.GetValues cannot be used in production code to build the filter list.
            Assert.False(typeof(CertificateKeyPairOrigin).IsEnum);
            Assert.False(typeof(KeyAlgorithm).IsEnum);
        }
    }
}
