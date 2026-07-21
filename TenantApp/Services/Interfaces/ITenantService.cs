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
        Task<TenantUsersResponse> GetUsersAsync(string tenantId, TenantAuthContext auth, CancellationToken cancellationToken = default);
        Task<TenantUsersResponse> PatchUsersAsync(string tenantId, TenantUsersPatchRequest request, string ifMatch, TenantAuthContext auth, CancellationToken cancellationToken = default);
        Task<TenantMarketCodesResponse> GetMarketCodesAsync(string tenantId, TenantAuthContext auth, CancellationToken cancellationToken = default);
        Task<TenantMarketCodesResponse> PatchMarketCodesAsync(string tenantId, TenantMarketCodesPatchRequest request, string ifMatch, TenantAuthContext auth, CancellationToken cancellationToken = default);
        Task<TenantLeaderMarketCodesResponse> GetLeaderMarketCodesAsync(string tenantId, string userId, TenantAuthContext auth, CancellationToken cancellationToken = default);
        Task<TenantLeaderMarketCodesResponse> PatchLeaderMarketCodesAsync(string tenantId, string userId, TenantLeaderMarketCodesPatchRequest request, string ifMatch, TenantAuthContext auth, CancellationToken cancellationToken = default);
    }
}
