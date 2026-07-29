using Taslow.Task.Model;

namespace Taslow.Task.Service.Interface;

public interface ITaskAuthorizationService
{
    TaskAuthContext Resolve(IDictionary<string, string> headers);

    void EnsureTenant(TaskAuthContext auth, string tenantId);

    void EnsureSelf(TaskAuthContext auth, string email);

    void EnsureProjectManager(TaskAuthContext auth);
}
