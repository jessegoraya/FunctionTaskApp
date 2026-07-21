using Taslow.Project.Model;

namespace Taslow.Project.Service.Interface;

public interface IProjectAuthorizationService
{
    ProjectAuthContext Resolve(IDictionary<string, string> headers);

    void EnsureCanCreate(ProjectAuthContext auth, string tenantId);

    void EnsureCanManage(ProjectAuthContext auth, string tenantId);
}
