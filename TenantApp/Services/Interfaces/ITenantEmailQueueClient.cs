using Taslow.Shared.Model;

namespace Taslow.Tenant.Service.Interface
{
    public interface ITenantEmailQueueClient
    {
        Task EnqueueAsync(TenantEmailExtractionQueueMessage message, CancellationToken cancellationToken = default);
    }
}
