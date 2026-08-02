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

    public Task<bool> IsTenantActiveAsync(string tenantId)
        => _projectDb.IsTenantActiveAsync(tenantId);

    public Task<object> GetProjectAssociationsAsync(string tenantId, string projectId, string mode, string role)
        => _projectDb.GetProjectAssociationsAsync(tenantId, projectId, mode, role);

    public Task<Dictionary<string, ProjectDTO>> GetProjectsByIdListAsync(List<string> projectIds, string tenantId)
        => _projectDb.GetProjectsByIdListAsync(projectIds, tenantId);

    public Task<ProjectAgentContextResponse> GetProjectAgentContextBatchAsync(ProjectAgentContextRequest request)
        => _projectDb.GetProjectAgentContextBatchAsync(request);

    public async Task<ProjectParticipantCandidateResponse> GetParticipantProjectCandidatesAsync(
        ProjectParticipantCandidateRequest request)
    {
        var participantEmails = request.ParticipantEmails
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var projects = await _projectDb.GetActiveProjectsByTenantAsync(request.TenantId);

        var candidates = projects
            .Select(project => new ProjectParticipantCandidate
            {
                ProjectId = project.Id,
                MatchedParticipantEmails = project.AssociatedPeople
                    .Concat(project.AssociatedManagers)
                    .Select(person => person.PersonEmail?.Trim().ToLowerInvariant() ?? string.Empty)
                    .Where(participantEmails.Contains)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(email => email, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .Where(candidate => candidate.MatchedParticipantEmails.Count > 0)
            .OrderByDescending(candidate => candidate.MatchedParticipantEmails.Count)
            .ThenBy(candidate => candidate.ProjectId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ProjectParticipantCandidateResponse
        {
            TenantId = request.TenantId,
            Projects = candidates
        };
    }

    public Task<bool> UpdateProjectClientDomainsAsync(ProjectClientDomainsPatchRequest request)
        => _projectDb.UpdateProjectClientDomainsAsync(request);

    public Task<ProjectScopeLinkResponse> LinkProjectScopeGroupTaskSetsAsync(ProjectScopeLinkRequest request)
        => _projectDb.LinkProjectScopeGroupTaskSetsAsync(request);

    public Task<List<string>> GetProjectIdsForManagerAsync(string manager, string tenantId)
        => _projectDb.GetProjectIdsForManagerAsync(manager, tenantId);
}
