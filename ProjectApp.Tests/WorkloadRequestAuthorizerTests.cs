using Taslow.Shared.Security;
using Xunit;

namespace ProjectApp.Tests;

public sealed class WorkloadRequestAuthorizerTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("email-ingestion", false)]
    [InlineData("EMAIL-INGESTION-RUNTIME", false)]
    [InlineData("email-ingestion-runtime", true)]
    [InlineData(" email-ingestion-runtime ", true)]
    public void EmailIngestionAuthorization_RequiresExactTrustedMarker(
        string? value,
        bool expected)
    {
        Assert.Equal(
            expected,
            WorkloadRequestAuthorizer.IsEmailIngestionAuthorized(value));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("email-ingestion-runtime", false)]
    [InlineData("EMAIL-E2E-TEST-RUNNER", false)]
    [InlineData("email-e2e-test-runner", true)]
    [InlineData(" email-e2e-test-runner ", true)]
    public void EmailE2ETestRunnerAuthorization_RequiresExactTrustedMarker(
        string? value,
        bool expected)
    {
        Assert.Equal(
            expected,
            WorkloadRequestAuthorizer.IsEmailE2ETestRunnerAuthorized(value));
    }
}
