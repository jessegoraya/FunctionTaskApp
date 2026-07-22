using Taslow.Tenant.Model;

namespace Taslow.Tenant.Service.Interface
{
    public interface IGraphNotificationValidator
    {
        Task<GraphNotificationRoute?> ValidateAsync(
            string clientState,
            string subscriptionId,
            CancellationToken cancellationToken = default);
    }
}
