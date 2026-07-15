using Newtonsoft.Json.Linq;
using Taslow.Project.DAL;
using Xunit;

namespace ProjectApp.Tests;

public class ActiveProjectMappingTests
{
    [Fact]
    public void ActiveProjectQuery_ShouldIncludeKnownTenantAndStatusCasings()
    {
        Assert.Contains("c.TenantID = @tenantId", DBUtil.ActiveProjectsByTenantQuery);
        Assert.Contains("c.tenantID = @tenantId", DBUtil.ActiveProjectsByTenantQuery);
        Assert.Contains("c.tenantId = @tenantId", DBUtil.ActiveProjectsByTenantQuery);
        Assert.Contains("LOWER(c.ProjectStatus) = 'active'", DBUtil.ActiveProjectsByTenantQuery);
        Assert.Contains("LOWER(c.projectStatus) = 'active'", DBUtil.ActiveProjectsByTenantQuery);
    }

    [Fact]
    public void MapActiveProject_ShouldNormalizeLegacyProjectShape()
    {
        var source = JObject.Parse(@"{
          ""id"": ""project-a"",
          ""projectNames"": ""ACME Recompete"",
          ""projectDescription"": ""Delivery support"",
          ""projectType"": ""Delivery"",
          ""marketCode"": ""CIVIL"",
          ""projectStatus"": ""Active"",
          ""tenantID"": ""tenant-a"",
          ""clientDomains"": [""client.example""],
          ""AssociatedManagers"": [
            {
              ""AssociatedPersonID"": ""35db68dd-3d1f-4d14-8d94-f6327ae19e98"",
              ""PersonName"": ""Evelyn Carter"",
              ""PersonEmail"": ""evelyn-carter@acme-consulting.example"",
              ""Role"": ""Manager""
            }
          ],
          ""ProjectScopes"": [
            {
              ""ScopeID"": ""scope-a"",
              ""ProjectScopeAreaTitle"": ""Mobilization"",
              ""ProjectScopeArea"": ""Start the project"",
              ""GroupTaskSetID"": ""gts-a""
            }
          ]
        }");

        var project = DBUtil.MapActiveProject(source);

        Assert.Equal("project-a", project.Id);
        Assert.Equal("ACME Recompete", project.ProjectName);
        Assert.Equal("Delivery", project.ProjectType);
        Assert.Equal("CIVIL", project.MarketCode);
        Assert.Equal("Active", project.ProjectStatus);
        Assert.Equal("tenant-a", project.TenantId);
        Assert.Equal("client.example", Assert.Single(project.ClientDomains));
        Assert.Equal("evelyn-carter@acme-consulting.example", Assert.Single(project.AssociatedManagers).PersonEmail);
        Assert.Equal("gts-a", Assert.Single(project.ProjectScopes).GroupTaskSetId);
    }
}
