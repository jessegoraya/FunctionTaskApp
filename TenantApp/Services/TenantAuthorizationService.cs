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
            var trustedContext = ResolveTrustedHeaderContext(headers);
            if (trustedContext != null)
            {
                return trustedContext;
            }

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
                TenantId = tenantId,
                Subject = $"dev:{role.ToLowerInvariant()}:{tenantId ?? "taslow"}",
                Provider = AuthProviders.Synthetic,
                IdentityMode = IdentityModes.Synthetic,
                Environment = TaslowEnvironments.Development,
                Impersonated = true,
                Roles = new List<string> { role.ToLowerInvariant() }
            };
        }

        private static TenantAuthContext? ResolveTrustedHeaderContext(IDictionary<string, string> headers)
        {
            headers.TryGetValue("x-taslow-tenant-id", out var tenantId);
            headers.TryGetValue("x-taslow-user-subject", out var subject);
            headers.TryGetValue("x-taslow-roles", out var rawRoles);

            if (string.IsNullOrWhiteSpace(subject) && string.IsNullOrWhiteSpace(rawRoles))
            {
                return null;
            }

            var roles = SplitHeaderList(rawRoles);
            if (roles.Count == 0)
            {
                throw new TenantApiException(
                    HttpStatusCode.Unauthorized,
                    AuthErrorCodes.TokenInvalid,
                    "Trusted auth context is missing role claims.");
            }

            var invalidRole = roles.FirstOrDefault(role => !TenantRoles.All.Contains(role));
            if (!string.IsNullOrWhiteSpace(invalidRole))
            {
                throw new TenantApiException(
                    HttpStatusCode.Unauthorized,
                    AuthErrorCodes.TokenInvalid,
                    $"Trusted auth context contains unsupported role: {invalidRole}.");
            }

            headers.TryGetValue("x-taslow-permissions", out var rawPermissions);
            headers.TryGetValue("x-taslow-provider", out var provider);
            headers.TryGetValue("x-taslow-identity-mode", out var identityMode);
            headers.TryGetValue("x-taslow-environment", out var environment);
            headers.TryGetValue("x-taslow-user-email", out var email);
            headers.TryGetValue("x-taslow-user-display-name", out var displayName);
            headers.TryGetValue("x-taslow-provider-tenant-id", out var providerTenantId);
            headers.TryGetValue("x-taslow-auth-context-id", out var jti);
            headers.TryGetValue("x-taslow-market-codes", out var rawLeaderMarketCodes);

            var primaryRole = roles.Contains(TenantRoles.TaslowAdmin, StringComparer.OrdinalIgnoreCase)
                ? TenantRoles.TaslowAdmin
                : roles[0].ToLowerInvariant();

            if (!primaryRole.Equals(TenantRoles.TaslowAdmin, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(tenantId))
            {
                throw new TenantApiException(
                    HttpStatusCode.Unauthorized,
                    AuthErrorCodes.TokenInvalid,
                    "Trusted auth context is missing tenant ID.");
            }

            return new TenantAuthContext
            {
                Role = primaryRole,
                TenantId = tenantId,
                Subject = subject ?? string.Empty,
                DisplayName = displayName ?? string.Empty,
                Email = email ?? string.Empty,
                Provider = string.IsNullOrWhiteSpace(provider) ? AuthProviders.Synthetic : provider,
                IdentityMode = string.IsNullOrWhiteSpace(identityMode) ? IdentityModes.Synthetic : identityMode,
                Environment = string.IsNullOrWhiteSpace(environment) ? TaslowEnvironments.Development : environment,
                ProviderTenantId = providerTenantId,
                Jti = jti,
                Roles = roles,
                Permissions = SplitHeaderList(rawPermissions),
                LeaderMarketCodes = SplitHeaderList(rawLeaderMarketCodes)
                    .Select(code => code.ToUpperInvariant())
                    .ToList()
            };
        }

        private static List<string> SplitHeaderList(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new List<string>();
            }

            return value
                .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim().ToLowerInvariant())
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public void EnsureCanList(TenantAuthContext auth)
        {
            if (!HasRole(auth, TenantRoles.TaslowAdmin))
            {
                throw new TenantApiException(
                    HttpStatusCode.Forbidden,
                    TenantErrorCodes.Forbidden,
                    "Only Taslow Admin can list tenants.");
            }
        }

        public void EnsureCanCreate(TenantAuthContext auth)
        {
            if (!HasRole(auth, TenantRoles.TaslowAdmin))
            {
                throw new TenantApiException(
                    HttpStatusCode.Forbidden,
                    TenantErrorCodes.Forbidden,
                    "Only Taslow Admin can create tenants.");
            }
        }

        public void EnsureCanReadOrUpdateTenant(TenantAuthContext auth, string tenantId)
        {
            if (HasRole(auth, TenantRoles.TaslowAdmin))
            {
                return;
            }

            if (HasRole(auth, TenantRoles.TenantAdmin)
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

        public void EnsureCanReadMarketCodes(TenantAuthContext auth, string tenantId)
        {
            if (HasRole(auth, TenantRoles.TaslowAdmin))
            {
                return;
            }

            var canRead = HasRole(auth, TenantRoles.TenantAdmin)
                || HasRole(auth, TenantRoles.TenantPm)
                || HasRole(auth, TenantRoles.TenantLeader);

            if (canRead
                && !string.IsNullOrWhiteSpace(auth.TenantId)
                && auth.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            throw new TenantApiException(
                HttpStatusCode.Forbidden,
                TenantErrorCodes.Forbidden,
                "Caller is not authorized to read Market Codes for this tenant.");
        }

        public void EnsureCanManageMarketCodes(TenantAuthContext auth, string tenantId)
        {
            EnsureCanReadOrUpdateTenant(auth, tenantId);
        }

        private static bool HasRole(TenantAuthContext auth, string role)
        {
            if (auth.Role.Equals(role, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return auth.Roles.Any(item => item.Equals(role, StringComparison.OrdinalIgnoreCase));
        }
    }
}
