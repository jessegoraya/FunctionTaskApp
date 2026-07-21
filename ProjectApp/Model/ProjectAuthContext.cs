namespace Taslow.Project.Model;

public sealed class ProjectAuthContext
{
    public string TenantId { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public List<string> Roles { get; init; } = new();
}
