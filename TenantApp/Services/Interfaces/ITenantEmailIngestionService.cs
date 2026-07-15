using Taslow.Shared.Model;
using Taslow.Tenant.Model;

namespace Taslow.Tenant.Service.Interface
{
    public interface ITenantEmailIngestionService
    {
        Task<TenantEmailIngestionIntakeResult> IntakeGraphEventAsync(
            GraphEmailEventIngestionRequest request,
            string correlationId,
            CancellationToken cancellationToken = default);

        Task ProcessExtractionMessageAsync(
            TenantEmailExtractionQueueMessage message,
            int dequeueCount,
            string correlationId,
            CancellationToken cancellationToken = default);
    }
}
