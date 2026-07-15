using Taslow.Tenant.Model;

namespace Taslow.Tenant.DAL.Interface
{
    public interface ITenantEmailIngestionStateRepository
    {
        Task<bool> TryCreateAsync(TenantEmailIngestionStateRecord record, CancellationToken cancellationToken = default);
        Task<TenantEmailIngestionStateRecord?> GetByIdAsync(string idempotencyKey, CancellationToken cancellationToken = default);
        Task UpsertAsync(TenantEmailIngestionStateRecord record, CancellationToken cancellationToken = default);
    }
}
