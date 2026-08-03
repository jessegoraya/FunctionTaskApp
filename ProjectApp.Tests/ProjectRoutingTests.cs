using Microsoft.Azure.Functions.Worker;
using Taslow.Project.Function;
using Xunit;

namespace ProjectApp.Tests;

public sealed class ProjectRoutingTests
{
    [Fact]
    public void ProjectCreateRoute_DoesNotCaptureLiteralBatchRoute()
    {
        var createRoute = GetRoute(
            typeof(ProjectManagementFunctions),
            nameof(ProjectManagementFunctions.CreateProjectV2Async));
        var batchRoute = GetRoute(
            typeof(ProjectTaskController),
            nameof(ProjectTaskController.GetProjectsBatch));

        Assert.Equal("projects/{tenantId:guid}", createRoute);
        Assert.Equal("projects/batch", batchRoute);
    }

    private static string? GetRoute(Type functionType, string methodName)
    {
        var method = functionType.GetMethod(methodName);
        return method!
            .GetParameters()[0]
            .GetCustomAttributes(typeof(HttpTriggerAttribute), inherit: false)
            .Cast<HttpTriggerAttribute>()
            .Single()
            .Route;
    }
}
