namespace Taslow.Tenant.Model
{
    public class TenantAuthContext
    {
        public string Role { get; set; } = string.Empty;
        public string? TenantId { get; set; }
    }
}
