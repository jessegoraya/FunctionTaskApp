using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Taslow.Tenant.DAL.Interface;
using Taslow.Tenant.Model;

namespace Taslow.Tenant.DAL
{
    public class TenantEmailIngestionStateRepository : ITenantEmailIngestionStateRepository
    {
        private readonly Container _container;

        public TenantEmailIngestionStateRepository(IConfiguration configuration)
        {
            var connection = configuration["CosmosDBConnection"];
            var databaseName = configuration["TenantCosmosDatabaseName"] ?? "bloomskyHealth";
            var containerName = configuration["TenantEmailIngestionStateContainerName"] ?? "TenantEmailIngestion";

            if (string.IsNullOrWhiteSpace(connection))
            {
                throw new InvalidOperationException("CosmosDBConnection setting is missing.");
            }

            var client = new CosmosClient(connection);
            _container = client.GetContainer(databaseName, containerName);
        }

        public async Task<bool> TryCreateAsync(TenantEmailIngestionStateRecord record, CancellationToken cancellationToken = default)
        {
            try
            {
                _ = await _container.CreateItemAsync(record, new PartitionKey(record.Id), cancellationToken: cancellationToken);
                return true;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                return false;
            }
        }

        public async Task<TenantEmailIngestionStateRecord?> GetByIdAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _container.ReadItemAsync<TenantEmailIngestionStateRecord>(
                    idempotencyKey,
                    new PartitionKey(idempotencyKey),
                    cancellationToken: cancellationToken);

                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task UpsertAsync(TenantEmailIngestionStateRecord record, CancellationToken cancellationToken = default)
        {
            _ = await _container.UpsertItemAsync(record, new PartitionKey(record.Id), cancellationToken: cancellationToken);
        }
    }
}
