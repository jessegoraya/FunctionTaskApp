using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using Taslow.Project.Function;
using Taslow.Project.Service;
using Taslow.Shared.Model;
using Xunit;

namespace ProjectApp.Tests;

public sealed class ProjectScopeSyncSecurityTests
{
    [Fact]
    public void ScopeLinkCallback_RequiresFunctionAuthorization()
    {
        var method = typeof(ProjectTaskController).GetMethod(
            nameof(ProjectTaskController.LinkProjectScopeGroupTaskSets));
        var trigger = method!
            .GetParameters()[0]
            .GetCustomAttributes(typeof(HttpTriggerAttribute), inherit: false)
            .Cast<HttpTriggerAttribute>()
            .Single();

        Assert.Equal(AuthorizationLevel.Function, trigger.AuthLevel);
    }

    [Theory]
    [InlineData("ProjectScopeLinkCallbackFunctionKey")]
    [InlineData("ScopeSyncCallbackSecret")]
    public async Task PublishAsync_FailsClosedWhenCallbackCredentialIsMissing(string missingSetting)
    {
        var settings = CompleteSettings();
        settings.Remove(missingSetting);
        var handler = new RecordingHandler();
        var publisher = CreatePublisher(settings, handler);

        var published = await publisher.PublishAsync(BuildPayload());

        Assert.False(published);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task PublishAsync_SeparatesFunctionKeyFromCallbackSharedSecret()
    {
        var handler = new RecordingHandler();
        var publisher = CreatePublisher(CompleteSettings(), handler);

        var published = await publisher.PublishAsync(BuildPayload());

        Assert.True(published);
        Assert.Equal(1, handler.CallCount);
        var body = JObject.Parse(handler.Body);
        Assert.Equal(
            "https://project.example/api/projects/tenant-a/project-a/scopes/link-gts?code=function-key",
            body.Value<string>("ProjectScopeLinkCallbackUrl"));
        Assert.Equal("callback-secret", body.Value<string>("ProjectScopeLinkSecret"));
    }

    private static Dictionary<string, string?> CompleteSettings() => new()
    {
        ["ScopeSyncOrchestrationEndpoint"] = "https://logic.example/invoke",
        ["ProjectScopeLinkCallbackBaseUrl"] = "https://project.example/api",
        ["ProjectScopeLinkCallbackFunctionKey"] = "function-key",
        ["ScopeSyncCallbackSecret"] = "callback-secret"
    };

    private static ProjectScopeSyncPublisher CreatePublisher(
        Dictionary<string, string?> settings,
        RecordingHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        return new ProjectScopeSyncPublisher(
            new HttpClient(handler),
            configuration,
            NullLogger<ProjectScopeSyncPublisher>.Instance);
    }

    private static ProjectScopeSyncPayload BuildPayload() => new()
    {
        TenantId = "tenant-a",
        ProjectId = "project-a",
        Added = new List<ProjectScopeSyncItem>
        {
            new()
            {
                ScopeId = "scope-a",
                ProjectScopeAreaTitle = "Scope A",
                ProjectScopeArea = "Deliver scope A"
            }
        }
    };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Body = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
