using System.Threading.Tasks;
using Taslow.Shared.Model;

namespace Taslow.Project.Service.Interface
{
    public interface IProjectScopeSyncPublisher
    {
        Task<bool> PublishAsync(ProjectScopeSyncPayload payload);
    }
}
