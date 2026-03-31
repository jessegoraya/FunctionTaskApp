using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Taslow.Shared.Model
{
    public static class TenantRoles
    {
        public const string TaslowAdmin = "taslow_admin";
        public const string TenantAdmin = "tenant_admin";

        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            TaslowAdmin,
            TenantAdmin
        };
    }

    public static class TenantStatuses
    {
        public const string Provisioning = "provisioning";
        public const string Active = "active";
        public const string Suspended = "suspended";
        public const string Terminated = "terminated";

        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            Provisioning,
            Active,
            Suspended,
            Terminated
        };
    }

    public static class TenantProviders
    {
        public const string Microsoft = "microsoft";
        public const string Google = "google";

        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            Microsoft,
            Google
        };
    }

    public static class TenantAdminModes
    {
        public const string ExternalGroup = "external_group";
        public const string ExplicitPrincipals = "explicit_principals";
        public const string Mixed = "mixed";

        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            ExternalGroup,
            ExplicitPrincipals,
            Mixed
        };
    }

    public static class BillingProviders
    {
        public const string Stripe = "stripe";
        public const string Wave = "wave";
        public const string Other = "other";

        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            Stripe,
            Wave,
            Other
        };
    }

    public static class BillingStatuses
    {
        public const string Trialing = "trialing";
        public const string Active = "active";
        public const string PastDue = "past_due";
        public const string Canceled = "canceled";

        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            Trialing,
            Active,
            PastDue,
            Canceled
        };
    }

    public static class IntegrationProviders
    {
        public const string Graph = "graph";
        public const string Gmail = "gmail";

        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            Graph,
            Gmail
        };
    }

    public static class TenantErrorCodes
    {
        public const string BadRequest = "BAD_REQUEST";
        public const string Unauthorized = "UNAUTHORIZED";
        public const string Forbidden = "FORBIDDEN";
        public const string NotFound = "NOT_FOUND";
        public const string Conflict = "CONFLICT";
        public const string PreconditionFailed = "PRECONDITION_FAILED";
        public const string ValidationFailed = "VALIDATION_FAILED";
        public const string ProviderMismatch = "TENANT_PROVIDER_MISMATCH";
        public const string ImmutableField = "TENANT_IMMUTABLE_FIELD";
        public const string MissingIfMatch = "TENANT_IF_MATCH_REQUIRED";
        public const string DuplicateTenant = "TENANT_DUPLICATE";
    }

    public class ApiErrorResponse
    {
        [JsonProperty("error")]
        public ApiError Error { get; set; } = new();
    }

    public class ApiError
    {
        [JsonProperty("code")]
        public string Code { get; set; } = TenantErrorCodes.BadRequest;

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonProperty("correlationId")]
        public string CorrelationId { get; set; } = string.Empty;

        [JsonProperty("details")]
        public List<string> Details { get; set; } = new();
    }

    public class TenantListResponse
    {
        [JsonProperty("items")]
        public List<TenantListItemDTO> Items { get; set; } = new();

        [JsonProperty("continuationToken")]
        public string? ContinuationToken { get; set; }
    }

    public class TenantListItemDTO
    {
        [JsonProperty("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonProperty("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonProperty("status")]
        public string Status { get; set; } = TenantStatuses.Provisioning;

        [JsonProperty("provider")]
        public string Provider { get; set; } = TenantProviders.Microsoft;

        [JsonProperty("updatedAt")]
        public string UpdatedAt { get; set; } = string.Empty;
    }

    public class TenantDetailResponse
    {
        [JsonProperty("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonProperty("etag")]
        public string ETag { get; set; } = string.Empty;

        [JsonProperty("data")]
        public TenantDocumentDTO Data { get; set; } = new();
    }

    public class TenantCreateRequest
    {
        [JsonProperty("display_name")]
        [Required]
        [MinLength(1)]
        [MaxLength(256)]
        public string DisplayName { get; set; } = string.Empty;

        [JsonProperty("legal_name")]
        [MaxLength(256)]
        public string? LegalName { get; set; }

        [JsonProperty("status")]
        public string? Status { get; set; }

        [JsonProperty("provider")]
        public string? Provider { get; set; }

        [JsonProperty("billing")]
        public TenantBillingPatchRequest? Billing { get; set; }

        [JsonProperty("administration")]
        public TenantAdministrationPatchRequest? Administration { get; set; }

        [JsonProperty("identity")]
        public TenantIdentityPatchRequest? Identity { get; set; }

        [JsonProperty("email_integration")]
        public TenantEmailIntegrationPatchRequest? EmailIntegration { get; set; }
    }

    public class TenantDetailsPatchRequest
    {
        [JsonProperty("display_name")]
        [MaxLength(256)]
        public string? DisplayName { get; set; }

        [JsonProperty("legal_name")]
        [MaxLength(256)]
        public string? LegalName { get; set; }

        [JsonProperty("status")]
        public string? Status { get; set; }

        [JsonProperty("entitlements_json")]
        public Dictionary<string, object>? EntitlementsJson { get; set; }
    }

    public class TenantBillingPatchRequest
    {
        [JsonProperty("provider")]
        public string? Provider { get; set; }

        [JsonProperty("provider_customer_id")]
        public string? ProviderCustomerId { get; set; }

        [JsonProperty("provider_subscription_id")]
        public string? ProviderSubscriptionId { get; set; }

        [JsonProperty("billing_status")]
        public string? BillingStatus { get; set; }

        [JsonProperty("plan_id")]
        public string? PlanId { get; set; }

        [JsonProperty("currency")]
        public string? Currency { get; set; }

        [JsonProperty("billing_email")]
        public string? BillingEmail { get; set; }
    }

    public class TenantAdministrationPatchRequest
    {
        [JsonProperty("mode")]
        public string? Mode { get; set; }

        [JsonProperty("provider")]
        public string? Provider { get; set; }

        [JsonProperty("external_group_key")]
        public string? ExternalGroupKey { get; set; }

        [JsonProperty("break_glass_enabled")]
        public bool? BreakGlassEnabled { get; set; }

        [JsonProperty("last_policy_verified_at")]
        public string? LastPolicyVerifiedAt { get; set; }
    }

    public class TenantIdentityPatchRequest
    {
        [JsonProperty("microsoft")]
        public TenantMicrosoftIdentityDTO? Microsoft { get; set; }

        [JsonProperty("google")]
        public TenantGoogleIdentityDTO? Google { get; set; }
    }

    public class TenantEmailIntegrationPatchRequest
    {
        [JsonProperty("graph")]
        public TenantGraphIntegrationDTO? Graph { get; set; }

        [JsonProperty("gmail")]
        public TenantGmailIntegrationDTO? Gmail { get; set; }

        [JsonProperty("mailboxStates")]
        public List<TenantMailboxStateDTO>? MailboxStates { get; set; }

        [JsonProperty("subscriptionRegistry")]
        public List<TenantSubscriptionRegistryItemDTO>? SubscriptionRegistry { get; set; }
    }

    public class TenantDocumentDTO
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("schema_version")]
        public string SchemaVersion { get; set; } = "1.0.0";

        [JsonProperty("tenant")]
        public TenantCoreDTO Tenant { get; set; } = new();

        [JsonProperty("billing")]
        public TenantBillingPatchRequest Billing { get; set; } = new();

        [JsonProperty("productivity_suite_administration")]
        public TenantAdministrationPatchRequest Administration { get; set; } = new();

        [JsonProperty("productivity_suite_identity")]
        public TenantIdentityPatchRequest Identity { get; set; } = new();

        [JsonProperty("productivity_suite_email_integration")]
        public TenantEmailIntegrationPatchRequest EmailIntegration { get; set; } = new()
        {
            Graph = new TenantGraphIntegrationDTO { Enabled = false },
            Gmail = new TenantGmailIntegrationDTO { Enabled = false },
            MailboxStates = new List<TenantMailboxStateDTO>(),
            SubscriptionRegistry = new List<TenantSubscriptionRegistryItemDTO>()
        };
    }

    public class TenantCoreDTO
    {
        [JsonProperty("tenant_id")]
        public string TenantId { get; set; } = string.Empty;

        [JsonProperty("status")]
        public string Status { get; set; } = TenantStatuses.Provisioning;

        [JsonProperty("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonProperty("legal_name")]
        public string? LegalName { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; } = string.Empty;

        [JsonProperty("updated_at")]
        public string UpdatedAt { get; set; } = string.Empty;

        [JsonProperty("entitlements_json")]
        public Dictionary<string, object> EntitlementsJson { get; set; } = new();
    }

    public class TenantMicrosoftIdentityDTO
    {
        [JsonProperty("microsoft_tid")]
        public string? MicrosoftTid { get; set; }

        [JsonProperty("oidc_issuer")]
        public string? OidcIssuer { get; set; }

        [JsonProperty("discovery_url")]
        public string? DiscoveryUrl { get; set; }

        [JsonProperty("admin_consent_required")]
        public bool? AdminConsentRequired { get; set; }

        [JsonProperty("admin_consent_granted")]
        public bool? AdminConsentGranted { get; set; }

        [JsonProperty("admin_consent_granted_at")]
        public string? AdminConsentGrantedAt { get; set; }

        [JsonProperty("consent_scopes_granted")]
        public List<string>? ConsentScopesGranted { get; set; }

        [JsonProperty("publisher_verification_state")]
        public string? PublisherVerificationState { get; set; }

        [JsonProperty("allowed_domains")]
        public List<string>? AllowedDomains { get; set; }
    }

    public class TenantGoogleIdentityDTO
    {
        [JsonProperty("hosted_domain_hd")]
        public string? HostedDomainHd { get; set; }

        [JsonProperty("workspace_customer_id")]
        public string? WorkspaceCustomerId { get; set; }

        [JsonProperty("verification_state")]
        public string? VerificationState { get; set; }

        [JsonProperty("consent_scopes_granted")]
        public List<string>? ConsentScopesGranted { get; set; }

        [JsonProperty("domain_wide_delegation_enabled")]
        public bool? DomainWideDelegationEnabled { get; set; }

        [JsonProperty("allowed_domains")]
        public List<string>? AllowedDomains { get; set; }
    }

    public class TenantGraphIntegrationDTO
    {
        [JsonProperty("enabled")]
        public bool? Enabled { get; set; }

        [JsonProperty("permission_mode")]
        public string? PermissionMode { get; set; }

        [JsonProperty("notification_url")]
        public string? NotificationUrl { get; set; }

        [JsonProperty("lifecycle_url")]
        public string? LifecycleUrl { get; set; }

        [JsonProperty("include_resource_data")]
        public bool? IncludeResourceData { get; set; }

        [JsonProperty("encryption_cert_id")]
        public string? EncryptionCertId { get; set; }

        [JsonProperty("encryption_private_key_ref")]
        public string? EncryptionPrivateKeyRef { get; set; }

        [JsonProperty("client_state_secret_ref")]
        public string? ClientStateSecretRef { get; set; }
    }

    public class TenantGmailIntegrationDTO
    {
        [JsonProperty("enabled")]
        public bool? Enabled { get; set; }

        [JsonProperty("pubsub_project_id")]
        public string? PubsubProjectId { get; set; }

        [JsonProperty("pubsub_topic")]
        public string? PubsubTopic { get; set; }

        [JsonProperty("pubsub_subscription")]
        public string? PubsubSubscription { get; set; }

        [JsonProperty("push_endpoint_url")]
        public string? PushEndpointUrl { get; set; }

        [JsonProperty("push_auth_enabled")]
        public bool? PushAuthEnabled { get; set; }

        [JsonProperty("expected_service_account_email")]
        public string? ExpectedServiceAccountEmail { get; set; }

        [JsonProperty("expected_audience")]
        public string? ExpectedAudience { get; set; }

        [JsonProperty("label_filtering_policy")]
        public List<string>? LabelFilteringPolicy { get; set; }
    }

    public class TenantMailboxStateDTO
    {
        [JsonProperty("mailbox_key")]
        public string MailboxKey { get; set; } = string.Empty;

        [JsonProperty("last_history_id")]
        public string? LastHistoryId { get; set; }

        [JsonProperty("watch_expiration_ms")]
        public long? WatchExpirationMs { get; set; }

        [JsonProperty("last_full_sync_at")]
        public string? LastFullSyncAt { get; set; }

        [JsonProperty("quota_backoff_until")]
        public string? QuotaBackoffUntil { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; } = "active";
    }

    public class TenantSubscriptionRegistryItemDTO
    {
        [JsonProperty("subscription_id")]
        public string SubscriptionId { get; set; } = string.Empty;

        [JsonProperty("provider")]
        public string Provider { get; set; } = IntegrationProviders.Graph;

        [JsonProperty("status")]
        public string Status { get; set; } = "active";

        [JsonProperty("next_renewal_at")]
        public string NextRenewalAt { get; set; } = string.Empty;

        [JsonProperty("last_renewal_attempt_at")]
        public string? LastRenewalAttemptAt { get; set; }

        [JsonProperty("expiration_datetime")]
        public string? ExpirationDateTime { get; set; }

        [JsonProperty("backoff_until")]
        public string? BackoffUntil { get; set; }

        [JsonProperty("health_state")]
        public string? HealthState { get; set; }
    }
}
