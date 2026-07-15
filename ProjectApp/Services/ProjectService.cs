using Taslow.Project.DAL.Interface;
using Taslow.Project.Model;
using Taslow.Project.Service.Interface;
using Taslow.Shared.Model;

namespace Taslow.Project.Service;

public sealed class ProjectService : IProjectService
{
    private readonly IProjectDBUtil _projectDb;

    public ProjectService(IProjectDBUtil projectDb)
    {
        _projectDb = projectDb;
    }

    public Task<bool> CreateAsync(TaskProject project)
    {
        project.tenantid = new SvcUtil().Create(project.tenantid);
        project.Id = Guid.NewGuid().ToString();
        return _projectDb.InsertProject(project);
    }

    public Task<List<ProjectDTO>> GetActiveProjectsByTenantAsync(string tenantId)
        => _projectDb.GetActiveProjectsByTenantAsync(tenantId);

    public Task<object> GetProjectAssociationsAsync(string tenantId, string projectId, string mode, string role)
        => _projectDb.GetProjectAssociationsAsync(tenantId, projectId, mode, role);

    public Task<Dictionary<string, ProjectDTO>> GetProjectsByIdListAsync(List<string> projectIds, string tenantId)
        => _projectDb.GetProjectsByIdListAsync(projectIds, tenantId);

    public Task<ProjectAgentContextResponse> GetProjectAgentContextBatchAsync(ProjectAgentContextRequest request)
        => _projectDb.GetProjectAgentContextBatchAsync(request);

    public Task<bool> UpdateProjectClientDomainsAsync(ProjectClientDomainsPatchRequest request)
        => _projectDb.UpdateProjectClientDomainsAsync(request);

    public Task<ProjectScopeLinkResponse> LinkProjectScopeGroupTaskSetsAsync(ProjectScopeLinkRequest request)
        => _projectDb.LinkProjectScopeGroupTaskSetsAsync(request);

    public Task<List<string>> GetProjectIdsForManagerAsync(string manager, string tenantId)
        => _projectDb.GetProjectIdsForManagerAsync(manager, tenantId);
}
