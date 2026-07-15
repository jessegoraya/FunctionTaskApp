using Taslow.Shared.Model;

namespace Taslow.Tenant.Service.Interface
{
    public interface IEmailExtractionClient
    {
        Task<TenantEmailExtractionInvokeResponse> InvokeAsync(
            TenantEmailExtractionQueueMessage message,
            string correlationId,
            CancellationToken cancellationToken = default);
    }
}
