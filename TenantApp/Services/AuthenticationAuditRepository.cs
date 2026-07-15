using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Taslow.Shared.Model;
using Taslow.Tenant.Service.Interface;

namespace Taslow.Tenant.Service
{
    public class AuthenticationAuditRepository : IAuthenticationAuditRepository
    {
        private readonly Container _container;

        public AuthenticationAuditRepository(IConfiguration configuration)
        {
            var connection = configuration["CosmosDBConnection"];
            var databaseName = configuration["TenantCosmosDatabaseName"] ?? "bloomskyHealth";
            var containerName = configuration["Auth:AuditContainerName"] ?? "AuthenticationAudit";

            if (string.IsNullOrWhiteSpace(connection))
            {
                throw new InvalidOperationException("CosmosDBConnection setting is missing.");
            }

            var client = new CosmosClient(connection);
            _container = client.GetContainer(databaseName, containerName);
        }

        public async Task CreateAsync(AuthenticationAuditRecord record, CancellationToken cancellationToken = default)
        {
            var partitionKey = string.IsNullOrWhiteSpace(record.TenantId)
                ? "taslow"
                : record.TenantId;

            await _container.CreateItemAsync(
                record,
                new PartitionKey(partitionKey),
                cancellationToken: cancellationToken);
        }
    }
}
