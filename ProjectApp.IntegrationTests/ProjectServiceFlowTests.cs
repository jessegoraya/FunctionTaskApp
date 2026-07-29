using Taslow.Project.DAL.Interface;
using Taslow.Project.Model;
using Taslow.Project.Service;
using Taslow.Shared.Model;
using Xunit;

namespace ProjectApp.IntegrationTests;

public class ProjectServiceFlowTests
{
    [Fact]
    public async Task CreateAndListFlow_ShouldPersistThroughRepositoryBoundary()
    {
        var repository = new InMemoryProjectDbUtil();
        var service = new ProjectService(repository);
        var project = new TaskProject
        {
            tenantid = "tenant-a",
            ProjectNames = "Alpha",
            projectstatus = "Active"
        };

        await service.CreateAsync(project);
        var projects = await service.GetActiveProjectsByTenantAsync(project.tenantid);

        var persisted = Assert.Single(projects);
        Assert.Equal(project.Id, persisted.Id);
        Assert.Equal("Alpha", persisted.ProjectName);
    }

    [Fact]
    public async Task ClientDomainAndScopeLinkFlow_ShouldRoundTrip()
    {
        var repository = new InMemoryProjectDbUtil();
        var service = new ProjectService(repository);
        var project = new TaskProject { tenantid = "tenant-a", ProjectNames = "Alpha" };
        await service.CreateAsync(project);

        var domainUpdated = await service.UpdateProjectClientDomainsAsync(new ProjectClientDomainsPatchRequest
        {
            TenantId = project.tenantid,
            ProjectId = project.Id,
            ClientDomains = new List<string> { "client.example" }
        });
        var linked = await service.LinkProjectScopeGroupTaskSetsAsync(new ProjectScopeLinkRequest
        {
            TenantId = project.tenantid,
            ProjectId = project.Id,
            Mappings = new List<ProjectScopeLinkMapping>
            {
                new() { ScopeId = "scope-a", GroupTaskSetId = "gts-a" }
            }
        });

        Assert.True(domainUpdated);
        Assert.True(linked.Updated);
        Assert.Equal("gts-a", Assert.Single(linked.Mappings).GroupTaskSetId);
    }

    private sealed class InMemoryProjectDbUtil : IProjectDBUtil
    {
        private readonly Dictionary<string, TaskProject> _projects = new(StringComparer.OrdinalIgnoreCase);

        public Task<bool> InsertProject(TaskProject item)
        {
            _projects[item.Id] = item;
            return Task.FromResult(true);
        }

        public Task<List<ProjectDTO>> GetActiveProjectsByTenantAsync(string tenantId)
            => Task.FromResult(_projects.Values
                .Where(project => string.Equals(project.tenantid, tenantId, StringComparison.OrdinalIgnoreCase))
                .Select(project => new ProjectDTO
                {
                    Id = project.Id,
                    ProjectName = project.ProjectNames,
                    ProjectStatus = project.projectstatus,
                    TenantId = project.tenantid
                })
                .ToList());

        public Task<bool> IsTenantActiveAsync(string tenantId)
            => Task.FromResult(true);

        public Task<bool> UpdateProjectClientDomainsAsync(ProjectClientDomainsPatchRequest request)
        {
            if (!_projects.TryGetValue(request.ProjectId, out var project) ||
                !string.Equals(project.tenantid, request.TenantId, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(false);
            }

            project.clientdomains = request.ClientDomains;
            return Task.FromResult(true);
        }

        public Task<ProjectScopeLinkResponse> LinkProjectScopeGroupTaskSetsAsync(ProjectScopeLinkRequest request)
        {
            var exists = _projects.TryGetValue(request.ProjectId, out var project)
                && string.Equals(project.tenantid, request.TenantId, StringComparison.OrdinalIgnoreCase);
            return Task.FromResult(new ProjectScopeLinkResponse
            {
                TenantId = request.TenantId,
                ProjectId = request.ProjectId,
                Updated = exists,
                Mappings = request.Mappings.Select(mapping => new ProjectScopeLinkResult
                {
                    ScopeId = mapping.ScopeId,
                    GroupTaskSetId = mapping.GroupTaskSetId,
                    OrchestrationRunId = mapping.OrchestrationRunId,
                    Status = exists ? "created" : "scope_not_found"
                }).ToList()
            });
        }

        public Task<Dictionary<string, TaskProject>> GetProjectDatabyProjectIDList(List<string> projectIds, string tenantid)
            => Task.FromResult(_projects
                .Where(pair => projectIds.Contains(pair.Key) && pair.Value.tenantid == tenantid)
                .ToDictionary(pair => pair.Key, pair => pair.Value));

        public Task<List<string>> GetProjectIdsForManagerAsync(string userEmail, string tenantid)
            => Task.FromResult(new List<string>());

        public Task<object> GetProjectAssociationsAsync(string tenantId, string projectId, string mode, string role)
            => Task.FromResult<object>(new object());

        public Task<Dictionary<string, ProjectDTO>> GetProjectsByIdListAsync(List<string> projectIds, string tenantId)
            => Task.FromResult(new Dictionary<string, ProjectDTO>());

        public Task<ProjectAgentContextResponse> GetProjectAgentContextBatchAsync(ProjectAgentContextRequest request)
            => Task.FromResult(new ProjectAgentContextResponse { TenantId = request.TenantId });
    }
}
