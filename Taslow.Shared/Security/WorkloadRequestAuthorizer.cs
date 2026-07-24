namespace Taslow.Shared.Security;

public static class WorkloadRequestAuthorizer
{
    public const string HeaderName = "x-taslow-internal-workload";
    public const string EmailIngestionValue = "email-ingestion-runtime";
    public const string EmailE2ETestRunnerValue = "email-e2e-test-runner";

    public static bool IsEmailIngestionAuthorized(string? value) =>
        string.Equals(
            value?.Trim(),
            EmailIngestionValue,
            StringComparison.Ordinal);

    public static bool IsEmailE2ETestRunnerAuthorized(string? value) =>
        string.Equals(
            value?.Trim(),
            EmailE2ETestRunnerValue,
            StringComparison.Ordinal);
}
