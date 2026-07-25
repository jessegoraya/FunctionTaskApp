using System;
using System.Linq;
using System.Reflection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Taslow.Tenant.Function;
using Xunit;

namespace TenantApp.Tests;

public sealed class TenantEmailIngestionFunctionMetadataTests
{
    [Theory]
    [InlineData(
        "IngestGraphSentEmailEvent",
        AuthorizationLevel.Anonymous,
        "email-ingestion/graph/events",
        "post")]
    [InlineData(
        "GetInternalTenantEmailIngestionEvidence",
        AuthorizationLevel.Function,
        "internal/email-ingestion/evidence/{idempotencyKey}",
        "get")]
    public void Functions_ExposeGovernedEmailIngestionRoutes(
        string functionName,
        AuthorizationLevel authorizationLevel,
        string route,
        string method)
    {
        var function = typeof(TenantEmailIngestionFunction)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(candidate => string.Equals(
                candidate.GetCustomAttribute<FunctionAttribute>()?.Name,
                functionName,
                StringComparison.Ordinal));
        var trigger = function
            .GetParameters()
            .Select(parameter => parameter.GetCustomAttribute<HttpTriggerAttribute>())
            .Single(attribute => attribute != null);

        Assert.Equal(authorizationLevel, trigger!.AuthLevel);
        Assert.Equal(route, trigger.Route);
        Assert.Contains(
            method,
            trigger.Methods ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null, "none")]
    [InlineData("Microsoft Graph message hydration failed.", "graph_hydration")]
    [InlineData("Foundry agent invocation failed with status 503.", "foundry_invocation")]
    [InlineData("Logic App task write failed.", "task_write")]
    [InlineData("Unexpected dependency response.", "unclassified")]
    public void ClassifyFailure_ReturnsOnlyNonSensitiveCategories(
        string? lastError,
        string expected)
    {
        Assert.Equal(expected, TenantEmailIngestionFunction.ClassifyFailure(lastError));
    }
}
