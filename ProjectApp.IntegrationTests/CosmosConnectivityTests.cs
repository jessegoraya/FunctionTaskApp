using Microsoft.Azure.Cosmos;
using Xunit;

namespace ProjectApp.IntegrationTests;

public class CosmosConnectivityTests
{
    [Fact]
    public async Task CosmosConnection_ShouldSucceed_WhenEmulatorConfigured()
    {
        var connection = Environment.GetEnvironmentVariable("COSMOS_EMULATOR_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
        {
            return;
        }

        using var client = new CosmosClient(connection);
        var account = await client.ReadAccountAsync();
        Assert.NotNull(account);
    }
}
