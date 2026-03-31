using System;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Xunit;

namespace TenantApp.IntegrationTests
{
    public class CosmosConnectivityTests
    {
        [Fact]
        public async Task CosmosConnection_ShouldSucceed_WhenEmulatorConfigured()
        {
            var connection = Environment.GetEnvironmentVariable("COSMOS_EMULATOR_CONNECTION");
            if (string.IsNullOrWhiteSpace(connection))
            {
                // Integration runs in nightly/main can provide this env var.
                return;
            }

            var client = new CosmosClient(connection);
            var account = await client.ReadAccountAsync();
            Assert.NotNull(account);
        }
    }
}
