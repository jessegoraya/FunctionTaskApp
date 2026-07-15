using Taslow.Shared.Model;

namespace Taslow.Tenant.Service.Interface
{
    public interface IAuthenticationAuditRepository
    {
        Task CreateAsync(AuthenticationAuditRecord record, CancellationToken cancellationToken = default);
    }
}
