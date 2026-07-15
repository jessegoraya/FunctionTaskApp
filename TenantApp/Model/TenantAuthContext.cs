namespace Taslow.Tenant.Model
{
    public class TenantAuthContext
    {
        public string Role { get; set; } = string.Empty;
        public string? TenantId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Provider { get; set; } = "synthetic";
        public string IdentityMode { get; set; } = "synthetic";
        public string Environment { get; set; } = "development";
        public string? ProviderTenantId { get; set; }
        public string? ProviderSubject { get; set; }
        public string? Jti { get; set; }
        public string? ExpiresAt { get; set; }
        public bool Impersonated { get; set; }
        public List<string> Roles { get; set; } = new();
        public List<string> Permissions { get; set; } = new();
        public List<string> LeaderMarketCodes { get; set; } = new();
    }
}
