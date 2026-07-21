using Taslow.Shared.Model;

namespace Taslow.Project.Service.Interface;

public interface IProjectRequestValidator
{
    bool IsValid(ProjectCreateRequest? request, string tenantId);

    bool IsValid(ProjectBatchRequest? request);

    bool IsValid(ProjectAgentContextRequest? request);

    bool IsValid(ProjectClientDomainsPatchRequest? request);

    bool IsValid(ProjectScopeLinkRequest? request, string tenantId, string projectId);

    bool IsCallbackAuthorized(string? expectedSecret, string? providedSecret);
}
