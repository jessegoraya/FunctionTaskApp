using Taslow.Shared.Model;

namespace Taslow.Tenant.Service.Interface
{
    public interface IMicrosoftGraphMessageClient
    {
        Task<TenantEmailExtractionQueueMessage> HydrateAsync(
            TenantEmailExtractionQueueMessage message,
            CancellationToken cancellationToken = default);
    }
}
