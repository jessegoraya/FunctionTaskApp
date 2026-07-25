using Taslow.Shared.Model;

namespace Taslow.Tenant.Service.Interface
{
    public interface IEmailTaskWriteClient
    {
        Task<TenantEmailTaskWriteResult> WriteAsync(
            TenantEmailExtractionQueueMessage message,
            TenantEmailExtractionInvokeResponse extraction,
            string correlationId,
            CancellationToken cancellationToken = default);
    }
}
