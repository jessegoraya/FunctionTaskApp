using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Taslow.Shared.Model
{
    public static class AuthProviders
    {
        public const string Synthetic = "synthetic";
        public const string Microsoft = "microsoft";
        public const string Google = "google";

        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            Synthetic,
            Microsoft,
            Google
        };
    }

    public static class IdentityModes
    {
        public const string Synthetic = "synthetic";
        public const string Integrated = "integrated";

        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            Synthetic,
            Integrated
        };
    }

    public static class TaslowEnvironments
    {
        public const string Development = "development";
        public const string Test = "test";
        public const string Production = "production";
    }

    public static class AuthPermissions
    {
        public const string BreakGlassReadonly = "break_glass_readonly";
        public const string BreakGlassWrite = "break_glass_write";
    }

    public static class TaslowClaimTypes
    {
        public const string TenantId = "tid";
        public const string Provider = "provider";
        public const string IdentityMode = "identity_mode";
        public const string Environment = "environment";
        public const string Roles = "roles";
        public const string Permissions = "permissions";
        public const string LeaderMarketCodes = "leader_market_codes";
        public const string ProviderTenantId = "provider_tid";
        public const string ProviderSubject = "provider_sub";
        public const string Impersonated = "impersonated";
    }

    public static class AuthErrorCodes
    {
        public const string Required = "AUTH_REQUIRED";
        public const string TokenInvalid = "AUTH_TOKEN_INVALID";
        public const string TokenExpired = "AUTH_TOKEN_EXPIRED";
        public const string TenantNotActive = "AUTH_TENANT_NOT_ACTIVE";
        public const string TenantNotFound = "AUTH_TENANT_NOT_FOUND";
        public const string ProviderRequired = "AUTH_PROVIDER_REQUIRED";
        public const string ProviderUnsupported = "AUTH_PROVIDER_UNSUPPORTED";
        public const string ProviderTenantMismatch = "AUTH_PROVIDER_TENANT_MISMATCH";
        public const string UserNotInTenant = "AUTH_USER_NOT_IN_TENANT";
        public const string UserNotSelectable = "AUTH_USER_NOT_SELECTABLE";
        public const string RoleForbidden = "AUTH_ROLE_FORBIDDEN";
        public const string SyntheticDisabled = "AUTH_SYNTHETIC_DISABLED";
        public const string SyntheticNotAllowedInProduction = "AUTH_SYNTHETIC_NOT_ALLOWED_IN_PRODUCTION";
        public const string ImpersonationNotAllowedInProduction = "AUTH_IMPERSONATION_NOT_ALLOWED_IN_PRODUCTION";
        public const string DevHeadersDisabled = "AUTH_DEV_HEADERS_DISABLED";
        public const string DirectoryLookupFailed = "AUTH_DIRECTORY_LOOKUP_FAILED";
    }

    public static class AuthAuditEventTypes
    {
        public const string LoginStarted = "login_started";
        public const string LoginSucceeded = "login_succeeded";
        public const string LoginFailed = "login_failed";
        public const string Logout = "logout";
        public const string TokenRejected = "token_rejected";
        public const string AccessDenied = "access_denied";
        public const string BreakGlassRequested = "break_glass_requested";
        public const string BreakGlassStarted = "break_glass_started";
        public const string BreakGlassEnded = "break_glass_ended";
    }

    public class AuthContextResponse
    {
        [JsonProperty("authenticated")]
        public bool Authenticated { get; set; }

        [JsonProperty("environment")]
        public string Environment { get; set; } = TaslowEnvironments.Development;

        [JsonProperty("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonProperty("tenantName")]
        public string TenantName { get; set; } = string.Empty;

        [JsonProperty("provider")]
        public string Provider { get; set; } = AuthProviders.Synthetic;

        [JsonProperty("identityMode")]
        public string IdentityMode { get; set; } = IdentityModes.Synthetic;

        [JsonProperty("roles")]
        public List<string> Roles { get; set; } = new();

        [JsonProperty("permissions")]
        public List<string> Permissions { get; set; } = new();

        [JsonProperty("leaderMarketCodes")]
        public List<string> LeaderMarketCodes { get; set; } = new();

        [JsonProperty("user")]
        public AuthUserContext User { get; set; } = new();

        [JsonProperty("expiresAt")]
        public string ExpiresAt { get; set; } = string.Empty;
    }

    public class AuthUserContext
    {
        [JsonProperty("subject")]
        public string Subject { get; set; } = string.Empty;

        [JsonProperty("providerTenantId")]
        public string? ProviderTenantId { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonProperty("email")]
        public string Email { get; set; } = string.Empty;
    }

    public class LoginOptionsResponse
    {
        [JsonProperty("items")]
        public List<TenantLoginOption> Items { get; set; } = new();
    }

    public class TenantLoginOption
    {
        [JsonProperty("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonProperty("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonProperty("status")]
        public string Status { get; set; } = TenantStatuses.Active;

        [JsonProperty("provider")]
        public string Provider { get; set; } = AuthProviders.Synthetic;

        [JsonProperty("identityMode")]
        public string IdentityMode { get; set; } = IdentityModes.Synthetic;

        [JsonProperty("loginEnabled")]
        public bool LoginEnabled { get; set; }
    }

    public class SelectableUsersResponse
    {
        [JsonProperty("items")]
        public List<SelectableUser> Items { get; set; } = new();
    }

    public class SelectableUser
    {
        [JsonProperty("tenantUserId")]
        public string TenantUserId { get; set; } = string.Empty;

        [JsonProperty("subject")]
        public string Subject { get; set; } = string.Empty;

        [JsonProperty("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonProperty("email")]
        public string Email { get; set; } = string.Empty;

        [JsonProperty("source")]
        public string Source { get; set; } = "manual";

        [JsonProperty("roles")]
        public List<string> Roles { get; set; } = new();

        [JsonProperty("primaryRole")]
        public string PrimaryRole { get; set; } = TenantRoles.TenantUser;

        [JsonProperty("provider")]
        public string Provider { get; set; } = AuthProviders.Synthetic;

        [JsonProperty("identityMode")]
        public string IdentityMode { get; set; } = IdentityModes.Synthetic;

        [JsonProperty("roleDerivationSummary")]
        public string? RoleDerivationSummary { get; set; }

        [JsonProperty("leaderMarketCodes")]
        public List<string> LeaderMarketCodes { get; set; } = new();
    }

    public class DevSessionRequest
    {
        [JsonProperty("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonProperty("identityMode")]
        public string IdentityMode { get; set; } = IdentityModes.Synthetic;

        [JsonProperty("selectedUserId")]
        public string SelectedUserId { get; set; } = string.Empty;

        [JsonProperty("user")]
        public AuthUserContext User { get; set; } = new();

        [Obsolete("Roles are resolved server-side from the selected tenant user catalog.")]
        [JsonProperty("requestedRole")]
        public string RequestedRole { get; set; } = TenantRoles.TenantUser;
    }

    public class DevSessionResponse
    {
        [JsonProperty("session")]
        public AuthContextResponse Session { get; set; } = new();

        [JsonProperty("accessToken")]
        public string AccessToken { get; set; } = string.Empty;
    }

    public class ProviderLoginStartRequest
    {
        [JsonProperty("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonProperty("provider")]
        public string Provider { get; set; } = AuthProviders.Microsoft;

        [JsonProperty("selectedUserId")]
        public string SelectedUserId { get; set; } = string.Empty;

        [JsonProperty("loginHint")]
        public string LoginHint { get; set; } = string.Empty;

        [JsonProperty("returnUrl")]
        public string ReturnUrl { get; set; } = "/tasks";
    }

    public class ProviderLoginStartResponse
    {
        [JsonProperty("provider")]
        public string Provider { get; set; } = AuthProviders.Microsoft;

        [JsonProperty("authorizationUrl")]
        public string AuthorizationUrl { get; set; } = string.Empty;

        [JsonProperty("expiresAt")]
        public string ExpiresAt { get; set; } = string.Empty;
    }

    public class ProviderLoginCompletion
    {
        [JsonProperty("session")]
        public AuthContextResponse Session { get; set; } = new();

        [JsonProperty("accessToken")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonProperty("returnUrl")]
        public string ReturnUrl { get; set; } = "/tasks";
    }

    public class AuthenticationAuditRecord
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("jti")]
        public string Jti { get; set; } = string.Empty;

        [JsonProperty("environment")]
        public string Environment { get; set; } = TaslowEnvironments.Development;

        [JsonProperty("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonProperty("provider")]
        public string Provider { get; set; } = AuthProviders.Synthetic;

        [JsonProperty("identityMode")]
        public string IdentityMode { get; set; } = IdentityModes.Synthetic;

        [JsonProperty("subject")]
        public string Subject { get; set; } = string.Empty;

        [JsonProperty("roles")]
        public List<string> Roles { get; set; } = new();

        [JsonProperty("issuedAt")]
        public string? IssuedAt { get; set; }

        [JsonProperty("expiresAt")]
        public string? ExpiresAt { get; set; }

        [JsonProperty("impersonated")]
        public bool Impersonated { get; set; }

        [JsonProperty("loginReason")]
        public string? LoginReason { get; set; }

        [JsonProperty("eventType")]
        public string EventType { get; set; } = AuthAuditEventTypes.LoginStarted;

        [JsonProperty("supportElevation")]
        public BreakGlassElevation? SupportElevation { get; set; }

        [JsonProperty("correlationId")]
        public string CorrelationId { get; set; } = string.Empty;

        [JsonProperty("createdAt")]
        public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("O");
    }

    public class BreakGlassElevation
    {
        [JsonProperty("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonProperty("scope")]
        public string? Scope { get; set; }

        [JsonProperty("permission")]
        public string Permission { get; set; } = AuthPermissions.BreakGlassReadonly;

        [JsonProperty("reason")]
        public string Reason { get; set; } = string.Empty;

        [JsonProperty("expiresAt")]
        public string ExpiresAt { get; set; } = string.Empty;
    }
}
