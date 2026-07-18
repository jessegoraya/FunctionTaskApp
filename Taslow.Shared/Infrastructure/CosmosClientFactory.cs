using Azure.Identity;
using Microsoft.Azure.Cosmos;

namespace Taslow.Shared.Infrastructure;

public static class CosmosClientFactory
{
    public static CosmosClient Create(Func<string, string?> getSetting)
    {
        ArgumentNullException.ThrowIfNull(getSetting);

        var connectionString = getSetting("CosmosDBConnection");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return new CosmosClient(connectionString);
        }

        var endpoint = getSetting("CosmosDBEndpoint");
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException(
                "Configure CosmosDBConnection for local development or CosmosDBEndpoint for managed identity.");
        }

        return new CosmosClient(endpoint, new DefaultAzureCredential());
    }
}
