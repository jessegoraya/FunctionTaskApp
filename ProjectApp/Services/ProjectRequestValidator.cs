using Taslow.Project.Service.Interface;
using Taslow.Shared.Model;

namespace Taslow.Project.Service;

public sealed class ProjectRequestValidator : IProjectRequestValidator
{
    public bool IsValid(ProjectCreateRequest? request, string tenantId)
    {
        if (request == null
            || string.IsNullOrWhiteSpace(tenantId)
            || string.IsNullOrWhiteSpace(request.ProjectName)
            || !ProjectTypes.All.Contains(request.ProjectType)
            || string.IsNullOrWhiteSpace(request.MarketCode)
            || request.Managers == null
            || request.Managers.Count == 0
            || request.Scopes == null
            || request.Scopes.Count == 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(request.TenantId)
            && !request.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var people = request.Managers.Concat(request.Members ?? new List<string>()).ToList();
        if (people.Any(email => !System.Net.Mail.MailAddress.TryCreate(email, out _))
            || people.Select(email => email.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != people.Count)
        {
            return false;
        }

        var scopeAreas = request.Scopes
            .Select(scope => scope.ProjectScopeArea?.Trim() ?? string.Empty)
            .ToList();
        return scopeAreas.All(area => !string.IsNullOrWhiteSpace(area))
            && scopeAreas.Distinct(StringComparer.OrdinalIgnoreCase).Count() == scopeAreas.Count;
    }

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
