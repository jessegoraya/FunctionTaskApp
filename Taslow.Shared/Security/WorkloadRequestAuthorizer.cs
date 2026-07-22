namespace Taslow.Shared.Security;

public static class WorkloadRequestAuthorizer
{
    public const string HeaderName = "x-taslow-internal-workload";
    public const string EmailIngestionValue = "email-ingestion-runtime";

    public static bool IsEmailIngestionAuthorized(string? value) =>
        string.Equals(
            value?.Trim(),
            EmailIngestionValue,
            StringComparison.Ordinal);
}
