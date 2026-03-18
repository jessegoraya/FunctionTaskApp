using System.Collections.Generic;
using System.Threading.Tasks;
using Taslow.Project.Model;
using Taslow.Shared.Model;

namespace Taslow.Project.DAL.Interface
{
    public interface IProjectDBUtil
    {
        Task<bool> InsertProject(TaskProject item);

        Task<Dictionary<string, TaskProject>> GetProjectDatabyProjectIDList(List<string> projectIds, string tenantid);

        Task<List<string>> GetProjectIdsForManagerAsync(string userEmail, string tenantid);

        Task<List<TaskProject>> GetActiveProjectsByTenantAsync(string tenantId);

        Task<object> GetProjectAssociationsAsync(string tenantId, string projectId, string mode, string role);

        Task<Dictionary<string, ProjectDTO>> GetProjectsByIdListAsync(List<string> projectIds, string tenantId);

        Task<ProjectDetailDTO> GetProjectDetailAsync(string tenantId, string projectId);

        Task<bool> IsManagerForProjectAsync(string tenantId, string projectId, string managerEmail);

        Task<ProjectDetailDTO> PatchProjectMetadataAsync(
            string tenantId,
            string projectId,
            ProjectMetadataPatchRequest request);

        Task<ProjectDetailDTO> PatchProjectAssociationsAsync(
            string tenantId,
            string projectId,
            ProjectAssociationPatchRequest request);

        Task<ProjectScopePatchResultDTO> PatchProjectScopesAsync(
            string tenantId,
            string projectId,
            ProjectScopePatchRequest request);
    }
}
