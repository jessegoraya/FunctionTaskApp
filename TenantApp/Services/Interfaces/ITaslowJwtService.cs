using Taslow.Tenant.Model;

namespace Taslow.Tenant.Service.Interface
{
    public interface ITaslowJwtService
    {
        string IssueToken(TenantAuthContext auth, DateTimeOffset expiresAt);
        TenantAuthContext ValidateToken(string token);
    }
}
