using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Taslow.Shared.Model;
using Taslow.Tenant.Service.Interface;
using Taslow.Shared.Infrastructure;

namespace Taslow.Tenant.Service
{
    public class AuthenticationAuditRepository : IAuthenticationAuditRepository
    {
        private readonly Container _container;

        public AuthenticationAuditRepository(IConfiguration configuration)
        {
            var databaseName = configuration["TenantCosmosDatabaseName"] ?? "bloomskyHealth";
            var containerName = configuration["Auth:AuditContainerName"] ?? "AuthenticationAudit";
            var client = CosmosClientFactory.Create(key => configuration[key]);
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
