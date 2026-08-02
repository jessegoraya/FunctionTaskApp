using System.Security.Cryptography;
using System.Text;
using Taslow.Project.DAL.Interface;
using Taslow.Project.Model;
using Taslow.Project.Service;
using Taslow.Shared.Model;
using Xunit;

namespace ProjectApp.Tests;

public class ProjectServiceTests
{
    [Fact]
    public async Task CreateAsync_ShouldAssignIdHashTenantAndInsert()
    {
        var repository = new RecordingProjectDbUtil();
        var service = new ProjectService(repository);
        var project = new TaskProject { tenantid = "tenant-a", ProjectNames = "Alpha" };

        var result = await service.CreateAsync(project);

        Assert.True(result);
        Assert.True(Guid.TryParse(project.Id, out _));
        Assert.Equal(Hash("tenant-a"), project.tenantid);
        Assert.Same(project, repository.InsertedProject);
    }

    [Fact]
    public async Task LinkScopesAsync_ShouldReturnRepositoryResultUnchanged()
    {
        var expected = new ProjectScopeLinkResponse
        {
            TenantId = "tenant-a",
            ProjectId = "project-a",
            Updated = true
        };
        var repository = new RecordingProjectDbUtil { ScopeLinkResponse = expected };
        var service = new ProjectService(repository);

        var actual = await service.LinkProjectScopeGroupTaskSetsAsync(new ProjectScopeLinkRequest());

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task IsTenantActiveAsync_ShouldReturnRepositoryResult()
    {
        var repository = new RecordingProjectDbUtil { TenantIsActive = true };
        var service = new ProjectService(repository);

        Assert.True(await service.IsTenantActiveAsync("tenant-a"));
    }

    [Fact]
    public async Task ParticipantCandidates_ShouldPreferProjectsWithTheMostParticipantOverlap()
    {
        var repository = new RecordingProjectDbUtil
        {
            ActiveProjects = new List<ProjectDTO>
            {
                new()
                {
                    Id = "education",
                    AssociatedPeople = new List<ProjectPersonDTO>
                    {
                        new() { PersonEmail = "alex@bloomsky.onmicrosoft.com" }
                    }
                },
                new()
                {
                    Id = "va-radiology",
                    AssociatedPeople = new List<ProjectPersonDTO>
                    {
                        new() { PersonEmail = "ALEX@bloomsky.onmicrosoft.com" },
                        new() { PersonEmail = "paco@bloomsky.onmicrosoft.com" }
                    }
                },
                new()
                {
                    Id = "unrelated",
                    AssociatedPeople = new List<ProjectPersonDTO>
                    {
                        new() { PersonEmail = "tessa@bloomsky.onmicrosoft.com" }
                    }
                }
            }
        };
        var service = new ProjectService(repository);

        var response = await service.GetParticipantProjectCandidatesAsync(
            new ProjectParticipantCandidateRequest
            {
                TenantId = "tenant-a",
                ParticipantEmails = new List<string>
                {
                    "alex@bloomsky.onmicrosoft.com",
                    "paco@bloomsky.onmicrosoft.com"
                }
            });

        Assert.Equal(new[] { "va-radiology", "education" }, response.Projects.Select(p => p.ProjectId));
        Assert.Equal(
            new[] { "alex@bloomsky.onmicrosoft.com", "paco@bloomsky.onmicrosoft.com" },
            response.Projects[0].MatchedParticipantEmails);
    }

    private static string Hash(string value)
        => BitConverter.ToString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class RecordingProjectDbUtil : IProjectDBUtil
    {
        public TaskProject? InsertedProject { get; private set; }

        public ProjectScopeLinkResponse ScopeLinkResponse { get; set; } = new();

        public bool TenantIsActive { get; set; }

        public List<ProjectDTO> ActiveProjects { get; set; } = new();

        public Task<bool> InsertProject(TaskProject item)
        {
            InsertedProject = item;
            return Task.FromResult(true);
        }

        public Task<Dictionary<string, TaskProject>> GetProjectDatabyProjectIDList(List<string> projectIds, string tenantid)
            => Task.FromResult(new Dictionary<string, TaskProject>());

        public Task<List<string>> GetProjectIdsForManagerAsync(string userEmail, string tenantid)
            => Task.FromResult(new List<string>());

        public Task<List<ProjectDTO>> GetActiveProjectsByTenantAsync(string tenantId)
            => Task.FromResult(ActiveProjects);

        public Task<bool> IsTenantActiveAsync(string tenantId)
            => Task.FromResult(TenantIsActive);

        public Task<object> GetProjectAssociationsAsync(string tenantId, string projectId, string mode, string role)
            => Task.FromResult<object>(new object());

        public Task<Dictionary<string, ProjectDTO>> GetProjectsByIdListAsync(List<string> projectIds, string tenantId)
            => Task.FromResult(new Dictionary<string, ProjectDTO>());

        public Task<ProjectAgentContextResponse> GetProjectAgentContextBatchAsync(ProjectAgentContextRequest request)
            => Task.FromResult(new ProjectAgentContextResponse());

        public Task<bool> UpdateProjectClientDomainsAsync(ProjectClientDomainsPatchRequest request)
            => Task.FromResult(true);

        public Task<ProjectScopeLinkResponse> LinkProjectScopeGroupTaskSetsAsync(ProjectScopeLinkRequest request)
            => Task.FromResult(ScopeLinkResponse);
    }
}
