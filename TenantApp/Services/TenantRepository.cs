using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Taslow.Shared.Model;
using Taslow.Tenant.DAL.Interface;
using Taslow.Tenant.Model;

namespace Taslow.Tenant.DAL
{
    public class TenantRepository : ITenantRepository
    {
        private readonly Container _container;

        public TenantRepository(IConfiguration configuration)
        {
            var connection = configuration["CosmosDBConnection"];
            var databaseName = configuration["TenantCosmosDatabaseName"] ?? "bloomskyHealth";
            var containerName = configuration["TenantCosmosContainerName"] ?? "Tenant";

            if (string.IsNullOrWhiteSpace(connection))
            {
                throw new InvalidOperationException("CosmosDBConnection setting is missing.");
            }

            var client = new CosmosClient(connection);
            _container = client.GetContainer(databaseName, containerName);
        }

        public async Task<(List<TenantDocumentDTO> Items, string? ContinuationToken)> ListAsync(TenantListQuery query, CancellationToken cancellationToken = default)
        {
            var filters = new List<string>();

            var status = query.Status;
            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                filters.Add("LOWER(c.tenant.status) = @status");
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                filters.Add("STARTSWITH(LOWER(c.tenant.display_name), @search)");
            }

            var sql = "SELECT * FROM c";
            if (filters.Count > 0)
            {
                sql += $" WHERE {string.Join(" AND ", filters)}";
            }

            sql += " ORDER BY c.tenant.updated_at DESC";
            var queryDefinition = new QueryDefinition(sql);
            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                queryDefinition.WithParameter("@status", status.ToLowerInvariant());
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                queryDefinition.WithParameter("@search", query.Search.ToLowerInvariant());
            }

            var requestOptions = new QueryRequestOptions
            {
                MaxItemCount = query.PageSize <= 0 ? 25 : Math.Min(query.PageSize, 100)
            };

            using var iterator = _container.GetItemQueryIterator<TenantDocumentDTO>(
                queryDefinition,
                query.ContinuationToken,
                requestOptions);

            if (!iterator.HasMoreResults)
            {
                return (new List<TenantDocumentDTO>(), null);
            }

            var page = await iterator.ReadNextAsync(cancellationToken);
            return (page.ToList(), page.ContinuationToken);
        }

        public async Task<(TenantDocumentDTO? Document, string? ETag)> GetByIdAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _container.ReadItemAsync<TenantDocumentDTO>(
                    tenantId,
                    new PartitionKey(tenantId),
                    cancellationToken: cancellationToken);

                return (response.Resource, response.ETag);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return (null, null);
            }
        }

        public async Task<(TenantDocumentDTO Document, string ETag)> CreateAsync(TenantDocumentDTO document, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _container.CreateItemAsync(
                    document,
                    new PartitionKey(document.Id),
                    cancellationToken: cancellationToken);

                return (response.Resource, response.ETag);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                throw new TenantApiException(HttpStatusCode.Conflict, TenantErrorCodes.DuplicateTenant, "Tenant already exists.");
            }
        }

        public async Task<(TenantDocumentDTO Document, string ETag)> ReplaceAsync(TenantDocumentDTO document, string ifMatchETag, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _container.ReplaceItemAsync(
                    document,
                    document.Id,
                    new PartitionKey(document.Id),
                    new ItemRequestOptions { IfMatchEtag = ifMatchETag },
                    cancellationToken);

                return (response.Resource, response.ETag);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                throw new TenantApiException(HttpStatusCode.PreconditionFailed, TenantErrorCodes.PreconditionFailed, "ETag mismatch.");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                throw new TenantApiException(HttpStatusCode.NotFound, TenantErrorCodes.NotFound, "Tenant not found.");
            }
        }
    }
}
