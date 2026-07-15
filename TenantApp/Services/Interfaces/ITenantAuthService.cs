using Taslow.Shared.Model;
using Taslow.Tenant.Model;

namespace Taslow.Tenant.Service.Interface
{
    public interface ITenantAuthService
    {
        Task<DevSessionResponse> CreateDevSessionAsync(DevSessionRequest request, string correlationId, CancellationToken cancellationToken = default);
        Task<ProviderLoginStartResponse> StartProviderLoginAsync(ProviderLoginStartRequest request, string correlationId, CancellationToken cancellationToken = default);
        Task<ProviderLoginCompletion> CompleteMicrosoftLoginAsync(string code, string state, string correlationId, CancellationToken cancellationToken = default);
        Task<AuthContextResponse> ResolveContextAsync(IDictionary<string, string> headers, bool allowDevHeaders, string correlationId, CancellationToken cancellationToken = default);
        Task<LoginOptionsResponse> GetLoginOptionsAsync(CancellationToken cancellationToken = default);
        Task<SelectableUsersResponse> GetSelectableUsersAsync(string tenantId, CancellationToken cancellationToken = default);
        Task RecordLogoutAsync(TenantAuthContext auth, string correlationId, CancellationToken cancellationToken = default);
    }
}
