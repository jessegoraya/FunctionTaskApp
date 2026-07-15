using Taslow.Project.Service;
using Taslow.Shared.Model;
using Xunit;

namespace ProjectApp.Tests;

public class ProjectRequestValidatorTests
{
    private readonly ProjectRequestValidator _validator = new();

    [Fact]
    public void BatchRequest_ShouldRequireTenantAndProjectIds()
    {
        Assert.False(_validator.IsValid((ProjectBatchRequest?)null));
        Assert.False(_validator.IsValid(new ProjectBatchRequest { TenantId = "tenant-a" }));
        Assert.True(_validator.IsValid(new ProjectBatchRequest
        {
            TenantId = "tenant-a",
            ProjectIds = new List<string> { "project-a" }
        }));
    }

    [Fact]
    public void AgentContextRequest_ShouldRequireTenantAndProjectIds()
    {
        Assert.False(_validator.IsValid((ProjectAgentContextRequest?)null));
        Assert.True(_validator.IsValid(new ProjectAgentContextRequest
        {
            TenantId = "tenant-a",
            ProjectIds = new List<string> { "project-a" }
        }));
    }

    [Fact]
    public void ClientDomainsPatch_ShouldRequireTenantProjectAndDomains()
    {
        Assert.False(_validator.IsValid(new ProjectClientDomainsPatchRequest
        {
            TenantId = "tenant-a",
            ProjectId = string.Empty
        }));

        Assert.True(_validator.IsValid(new ProjectClientDomainsPatchRequest
        {
            TenantId = "tenant-a",
            ProjectId = "project-a",
            ClientDomains = new List<string>()
        }));
    }

    [Fact]
    public void ScopeLink_ShouldRequireMatchingRouteAndAtLeastOneMapping()
    {
        var request = new ProjectScopeLinkRequest
        {
            TenantId = "tenant-a",
            ProjectId = "project-a",
            Mappings = new List<ProjectScopeLinkMapping>
            {
                new() { ScopeId = "scope-a", GroupTaskSetId = "gts-a" }
            }
        };

        Assert.True(_validator.IsValid(request, "TENANT-A", "PROJECT-A"));
        Assert.False(_validator.IsValid(request, "tenant-b", "project-a"));
        request.Mappings.Clear();
        Assert.False(_validator.IsValid(request, "tenant-a", "project-a"));
    }

    [Fact]
    public void CallbackAuthorization_ShouldRequireConfiguredExactSecret()
    {
        Assert.False(_validator.IsCallbackAuthorized(null, "secret"));
        Assert.False(_validator.IsCallbackAuthorized("secret", "SECRET"));
        Assert.True(_validator.IsCallbackAuthorized("secret", "secret"));
    }
}
