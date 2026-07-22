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
}
