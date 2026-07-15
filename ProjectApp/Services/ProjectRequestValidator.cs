using Taslow.Project.Service.Interface;
using Taslow.Shared.Model;

namespace Taslow.Project.Service;

public sealed class ProjectRequestValidator : IProjectRequestValidator
{
    public bool IsValid(ProjectBatchRequest? request)
        => request != null
           && !string.IsNullOrWhiteSpace(request.TenantId)
           && request.ProjectIds != null
           && request.ProjectIds.Any();

    public bool IsValid(ProjectAgentContextRequest? request)
        => request != null
           && !string.IsNullOrWhiteSpace(request.TenantId)
           && request.ProjectIds != null
           && request.ProjectIds.Any();

    public bool IsValid(ProjectClientDomainsPatchRequest? request)
        => request != null
           && !string.IsNullOrWhiteSpace(request.TenantId)
           && !string.IsNullOrWhiteSpace(request.ProjectId)
           && request.ClientDomains != null;

    public bool IsValid(ProjectScopeLinkRequest? request, string tenantId, string projectId)
        => request != null
           && request.Mappings != null
           && request.Mappings.Any()
           && string.Equals(request.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)
           && string.Equals(request.ProjectId, projectId, StringComparison.OrdinalIgnoreCase);

    public bool IsCallbackAuthorized(string? expectedSecret, string? providedSecret)
        => !string.IsNullOrWhiteSpace(expectedSecret)
           && string.Equals(expectedSecret, providedSecret, StringComparison.Ordinal);
}
