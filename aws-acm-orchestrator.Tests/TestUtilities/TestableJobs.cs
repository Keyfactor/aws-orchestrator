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
using Keyfactor.Extensions.Orchestrator.Aws.Acm.Jobs;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;

namespace Keyfactor.Extensions.Orchestrator.Aws.Acm.Tests.TestUtilities
{
    /// <summary>
    /// Exposes <see cref="Inventory.BuildInventoryItem"/> (protected virtual) and lets a test inject a
    /// fake ACM client into the internal AcmClient field.
    ///
    /// Note the asymmetry: BuildInventoryItem reads AcmClient, but PerformInventory *reassigns* it to a
    /// real AmazonCertificateManagerClient on entry. So item-level behaviour is unit-testable while
    /// PerformInventory as a whole is not - see FilterParityTests for what that costs.
    /// </summary>
    internal sealed class TestableInventory : Inventory
    {
        public TestableInventory(IAmazonCertificateManager acm, ITestOutputHelper output = null)
            : base(new Mock<IPAMSecretResolver>().Object, NullLoggerOf<Inventory>(output))
        {
            if (output != null) Logger = new XunitLogger(nameof(Inventory), output);
            AcmClient = acm;
        }

        public CurrentInventoryItem Build(string alias, CertificateType certificateType)
            => BuildInventoryItem(alias, certificateType);

        private static ILogger<Inventory> NullLoggerOf<T>(ITestOutputHelper output)
            => output == null
                ? (ILogger<Inventory>)Microsoft.Extensions.Logging.Abstractions.NullLogger<Inventory>.Instance
                : new XunitLogger<Inventory>(output);
    }

    /// <summary>Constructs Management without touching AWS, for its pure helpers.</summary>
    internal sealed class TestableManagement
    {
        public static Management Create(ITestOutputHelper output = null)
        {
            var mgmt = new Management(new Mock<IPAMSecretResolver>().Object,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Management>.Instance);
            if (output != null) mgmt.Logger = new XunitLogger(nameof(Management), output);
            return mgmt;
        }
    }

    /// <summary>Constructs Discovery without touching AWS, for its pure helpers.</summary>
    internal sealed class TestableDiscovery
    {
        public static Discovery Create(ITestOutputHelper output = null)
        {
            var disc = new Discovery(new Mock<IPAMSecretResolver>().Object,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Discovery>.Instance);
            if (output != null) disc.Logger = new XunitLogger(nameof(Discovery), output);
            return disc;
        }
    }
}
