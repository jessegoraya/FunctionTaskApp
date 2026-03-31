using System.Net;
using Taslow.Shared.Model;
using Taslow.Tenant.Model;
using Taslow.Tenant.Service.Interface;

namespace Taslow.Tenant.Service
{
    public class TenantAuthorizationService : ITenantAuthorizationService
    {
        public TenantAuthContext ResolveAuthContext(IDictionary<string, string> headers, bool allowDevHeaders)
        {
            headers.TryGetValue("x-taslow-dev-role", out var role);
            headers.TryGetValue("x-taslow-dev-tenant-id", out var tenantId);

            if (!allowDevHeaders && (!string.IsNullOrWhiteSpace(role) || !string.IsNullOrWhiteSpace(tenantId)))
            {
                throw new TenantApiException(
                    HttpStatusCode.Unauthorized,
                    TenantErrorCodes.Unauthorized,
                    "Development auth headers are not allowed in this environment.");
            }

            if (!allowDevHeaders)
            {
                throw new TenantApiException(
                    HttpStatusCode.Unauthorized,
                    TenantErrorCodes.Unauthorized,
                    "Authentication is not configured for this environment.");
            }

            if (string.IsNullOrWhiteSpace(role))
            {
                throw new TenantApiException(
                    HttpStatusCode.Unauthorized,
                    TenantErrorCodes.Unauthorized,
                    "Missing required header: x-taslow-dev-role.");
            }

            if (!TenantRoles.All.Contains(role))
            {
                throw new TenantApiException(
                    HttpStatusCode.Unauthorized,
                    TenantErrorCodes.Unauthorized,
                    "Invalid role in x-taslow-dev-role.");
            }

            if (role.Equals(TenantRoles.TenantAdmin, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(tenantId))
            {
                throw new TenantApiException(
                    HttpStatusCode.Unauthorized,
                    TenantErrorCodes.Unauthorized,
                    "Tenant admin role requires x-taslow-dev-tenant-id.");
            }

            return new TenantAuthContext
            {
                Role = role.ToLowerInvariant(),
                TenantId = tenantId
            };
        }

        public void EnsureCanList(TenantAuthContext auth)
        {
            if (!auth.Role.Equals(TenantRoles.TaslowAdmin, StringComparison.OrdinalIgnoreCase))
            {
                throw new TenantApiException(
                    HttpStatusCode.Forbidden,
                    TenantErrorCodes.Forbidden,
                    "Only Taslow Admin can list tenants.");
            }
        }

        public void EnsureCanCreate(TenantAuthContext auth)
        {
            if (!auth.Role.Equals(TenantRoles.TaslowAdmin, StringComparison.OrdinalIgnoreCase))
            {
                throw new TenantApiException(
                    HttpStatusCode.Forbidden,
                    TenantErrorCodes.Forbidden,
                    "Only Taslow Admin can create tenants.");
            }
        }

        public void EnsureCanReadOrUpdateTenant(TenantAuthContext auth, string tenantId)
        {
            if (auth.Role.Equals(TenantRoles.TaslowAdmin, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (auth.Role.Equals(TenantRoles.TenantAdmin, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(auth.TenantId)
                && auth.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            throw new TenantApiException(
                HttpStatusCode.Forbidden,
                TenantErrorCodes.Forbidden,
                "Caller is not authorized for this tenant.");
        }
    }
}
