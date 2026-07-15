using System;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Xunit;

namespace FunctionTaskApp.IntegrationTests
{
    public class CosmosConnectivityTests
    {
        [Fact]
        public async Task CosmosConnection_ShouldSucceed_WhenTaskCosmosConnectionIsConfigured()
        {
            var connection = Environment.GetEnvironmentVariable("TaskCosmosConnection")
                ?? Environment.GetEnvironmentVariable("CosmosDBConnection");

            if (string.IsNullOrWhiteSpace(connection))
            {
                return;
            }

            var databaseName = Environment.GetEnvironmentVariable("TaskCosmosDatabaseName") ?? "bloomskyHealth";

            using var client = new CosmosClient(connection);
            var database = client.GetDatabase(databaseName);

            await database.ReadAsync();
        }
    }
}
