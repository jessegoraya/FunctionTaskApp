using Taslow.Project.Model;
using Taslow.Shared.Model;

namespace Taslow.Project.Service.Interface;

public interface IProjectService
{
    Task<bool> CreateAsync(TaskProject project);

    Task<List<ProjectDTO>> GetActiveProjectsByTenantAsync(string tenantId);

    Task<object> GetProjectAssociationsAsync(string tenantId, string projectId, string mode, string role);

    Task<Dictionary<string, ProjectDTO>> GetProjectsByIdListAsync(List<string> projectIds, string tenantId);

    Task<ProjectAgentContextResponse> GetProjectAgentContextBatchAsync(ProjectAgentContextRequest request);

    Task<bool> UpdateProjectClientDomainsAsync(ProjectClientDomainsPatchRequest request);

    Task<ProjectScopeLinkResponse> LinkProjectScopeGroupTaskSetsAsync(ProjectScopeLinkRequest request);

    Task<List<string>> GetProjectIdsForManagerAsync(string manager, string tenantId);
}
