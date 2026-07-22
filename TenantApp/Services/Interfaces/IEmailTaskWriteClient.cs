using Taslow.Shared.Model;

namespace Taslow.Tenant.Service.Interface
{
    public interface IEmailTaskWriteClient
    {
        Task<int> WriteAsync(
            TenantEmailExtractionQueueMessage message,
            TenantEmailExtractionInvokeResponse extraction,
            string correlationId,
            CancellationToken cancellationToken = default);
    }
}
