using Taslow.Shared.Model;

namespace Taslow.Tenant.Service.Interface
{
    public interface ITenantUserCatalogService
    {
        Task<IReadOnlyList<SelectableUser>> GetSelectableUsersAsync(
            string tenantId,
            CancellationToken cancellationToken = default);
    }
}
