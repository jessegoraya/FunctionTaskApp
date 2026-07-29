namespace Taslow.Task.Model;

public sealed class TaskAuthContext
{
    public string TenantId { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string AccessToken { get; init; } = string.Empty;

    public List<string> Roles { get; init; } = new();

    public List<string> LeaderMarketCodes { get; init; } = new();
}
