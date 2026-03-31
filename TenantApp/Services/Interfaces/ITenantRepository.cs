using Taslow.Shared.Model;
using Taslow.Tenant.Model;

namespace Taslow.Tenant.DAL.Interface
{
    public interface ITenantRepository
    {
        Task<(List<TenantDocumentDTO> Items, string? ContinuationToken)> ListAsync(TenantListQuery query, CancellationToken cancellationToken = default);
        Task<(TenantDocumentDTO? Document, string? ETag)> GetByIdAsync(string tenantId, CancellationToken cancellationToken = default);
        Task<(TenantDocumentDTO Document, string ETag)> CreateAsync(TenantDocumentDTO document, CancellationToken cancellationToken = default);
        Task<(TenantDocumentDTO Document, string ETag)> ReplaceAsync(TenantDocumentDTO document, string ifMatchETag, CancellationToken cancellationToken = default);
    }
}
