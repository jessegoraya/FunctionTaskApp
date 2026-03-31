using Taslow.Shared.Model;
using Taslow.Tenant.Model;

namespace Taslow.Tenant.Service.Interface
{
    public interface ITenantService
    {
        Task<TenantListResponse> ListAsync(TenantListQuery query, TenantAuthContext auth, CancellationToken cancellationToken = default);
        Task<TenantDetailResponse> GetByIdAsync(string tenantId, TenantAuthContext auth, CancellationToken cancellationToken = default);
        Task<TenantDetailResponse> CreateAsync(TenantCreateRequest request, TenantAuthContext auth, CancellationToken cancellationToken = default);
        Task<TenantDetailResponse> PatchTenantAsync(string tenantId, TenantDetailsPatchRequest request, string ifMatch, TenantAuthContext auth, CancellationToken cancellationToken = default);
        Task<TenantDetailResponse> PatchBillingAsync(string tenantId, TenantBillingPatchRequest request, string ifMatch, TenantAuthContext auth, CancellationToken cancellationToken = default);
        Task<TenantDetailResponse> PatchAdministrationAsync(string tenantId, TenantAdministrationPatchRequest request, string ifMatch, TenantAuthContext auth, CancellationToken cancellationToken = default);
        Task<TenantDetailResponse> PatchIdentityAsync(string tenantId, TenantIdentityPatchRequest request, string ifMatch, TenantAuthContext auth, CancellationToken cancellationToken = default);
        Task<TenantDetailResponse> PatchEmailIntegrationAsync(string tenantId, TenantEmailIntegrationPatchRequest request, string ifMatch, TenantAuthContext auth, CancellationToken cancellationToken = default);
    }
}
