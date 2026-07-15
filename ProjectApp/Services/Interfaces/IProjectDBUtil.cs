using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        Task<List<ProjectDTO>> GetActiveProjectsByTenantAsync(string tenantId);

        Task<object> GetProjectAssociationsAsync(string tenantId, string projectId, string mode, string role);

        Task<Dictionary<string, ProjectDTO>> GetProjectsByIdListAsync(List<string> projectIds, string tenantId);

        Task<ProjectAgentContextResponse> GetProjectAgentContextBatchAsync(ProjectAgentContextRequest request);

        Task<bool> UpdateProjectClientDomainsAsync(ProjectClientDomainsPatchRequest request);

        Task<ProjectScopeLinkResponse> LinkProjectScopeGroupTaskSetsAsync(ProjectScopeLinkRequest request);

        Task<ProjectDetailDTO?> GetProjectDetailAsync(string tenantId, string projectId)
            => throw new NotSupportedException();

        Task<bool> IsManagerForProjectAsync(string tenantId, string projectId, string managerEmail)
            => throw new NotSupportedException();

        Task<ProjectDetailDTO> PatchProjectMetadataAsync(
            string tenantId,
            string projectId,
            ProjectMetadataPatchRequest request)
            => throw new NotSupportedException();

        Task<ProjectDetailDTO> PatchProjectAssociationsAsync(
            string tenantId,
            string projectId,
            ProjectAssociationPatchRequest request)
            => throw new NotSupportedException();

        Task<ProjectScopePatchResultDTO> PatchProjectScopesAsync(
            string tenantId,
            string projectId,
            ProjectScopePatchRequest request)
            => throw new NotSupportedException();

        Task<ProjectScopeGtsLinkResultDTO> LinkScopeGroupTaskSetsAsync(
            string tenantId,
            string projectId,
            ProjectScopeGtsLinkRequest request)
            => throw new NotSupportedException();

    }
}
