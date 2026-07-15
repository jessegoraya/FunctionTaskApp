using Taslow.Tenant.Model;

namespace Taslow.Tenant.Service.Interface
{
    public interface ITenantAuthorizationService
    {
        TenantAuthContext ResolveAuthContext(IDictionary<string, string> headers, bool allowDevHeaders);
        void EnsureCanList(TenantAuthContext auth);
        void EnsureCanCreate(TenantAuthContext auth);
        void EnsureCanReadOrUpdateTenant(TenantAuthContext auth, string tenantId);
        void EnsureCanReadMarketCodes(TenantAuthContext auth, string tenantId);
        void EnsureCanManageMarketCodes(TenantAuthContext auth, string tenantId);
    }
}
