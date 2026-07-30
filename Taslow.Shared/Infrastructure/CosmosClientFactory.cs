using Azure.Identity;
using Microsoft.Azure.Cosmos;

namespace Taslow.Shared.Infrastructure;

public static class CosmosClientFactory
{
    public static CosmosClient Create(Func<string, string?> getSetting)
    {
        ArgumentNullException.ThrowIfNull(getSetting);

        var options = CreateOptions(getSetting);
        var connectionString = getSetting("CosmosDBConnection");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return new CosmosClient(connectionString, options);
        }

        var endpoint = getSetting("CosmosDBEndpoint");
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException(
                "Configure CosmosDBConnection for local development or CosmosDBEndpoint for managed identity.");
        }

        return new CosmosClient(endpoint, new DefaultAzureCredential(), options);
    }

    private static CosmosClientOptions CreateOptions(Func<string, string?> getSetting)
    {
        var configuredMode = getSetting("CosmosDBConnectionMode");
        if (string.IsNullOrWhiteSpace(configuredMode))
        {
            return new CosmosClientOptions();
        }

        if (!Enum.TryParse<ConnectionMode>(configuredMode, true, out var connectionMode) ||
            connectionMode is not (ConnectionMode.Direct or ConnectionMode.Gateway))
        {
            throw new InvalidOperationException(
                "CosmosDBConnectionMode must be either Direct or Gateway.");
        }

        return new CosmosClientOptions
        {
            ConnectionMode = connectionMode
        };
    }
}
