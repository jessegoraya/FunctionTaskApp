using Newtonsoft.Json.Linq;
using Taslow.Project.DAL;
using Taslow.Shared.Model;
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
    public void ManagerProjectsQuery_ShouldUseStoredPersonEmailCasingAndLegacyFallbacks()
    {
        Assert.Contains("m.PersonEmail = @email", DBUtil.ProjectIdsForManagerQuery);
        Assert.Contains("m.personEmail = @email", DBUtil.ProjectIdsForManagerQuery);
        Assert.Contains("p.tenantID = @tenantID", DBUtil.ProjectIdsForManagerQuery);
    }

    [Fact]
    public void ProjectsByIdQuery_ShouldMatchCanonicalCosmosIdAndLegacyAliases()
    {
        Assert.Contains("ARRAY_CONTAINS(@ids, c.id)", DBUtil.ProjectsByIdQuery);
        Assert.Contains("ARRAY_CONTAINS(@ids, c.ProjectID)", DBUtil.ProjectsByIdQuery);
        Assert.Contains("ARRAY_CONTAINS(@ids, c.projectId)", DBUtil.ProjectsByIdQuery);
        Assert.Contains("ARRAY_CONTAINS(@ids, c.projectid)", DBUtil.ProjectsByIdQuery);
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

    [Fact]
    public void MapAgentContextProject_ShouldNormalizeCamelCaseScopeShape()
    {
        var source = JObject.Parse(@"{
          ""id"": ""project-a"",
          ""projectName"": ""ACME Recompete"",
          ""projectDescription"": ""Delivery support"",
          ""projectStatus"": ""Active"",
          ""projectScopes"": [
            {
              ""scopeId"": ""scope-a"",
              ""projectScopeAreaTitle"": ""Scope/General Description"",
              ""projectScopeArea"": ""General delivery requirements."",
              ""groupTaskSetId"": ""gts-a""
            }
          ]
        }");

        var project = DBUtil.MapAgentContextProject(
            source,
            new ProjectAgentContextRequest
            {
                TenantId = "tenant-a",
                ProjectIds = new List<string> { "project-a" },
                IncludeScopes = true
            });

        var scope = Assert.Single(project.Scopes);
        Assert.Equal("scope-a", scope.ScopeId);
        Assert.Equal("Scope/General Description", scope.ScopeTitle);
        Assert.Equal("General delivery requirements.", scope.ScopeDescription);
        Assert.Equal("gts-a", scope.GroupTaskSetId);
    }

    [Fact]
    public void EnrichAgentContextDisplayNames_ShouldUseCanonicalTenantDirectoryNames()
    {
        var project = new ProjectAgentContextProject
        {
            AssociatedPeople =
            {
                new ProjectAgentContextPerson
                {
                    Email = "bebright@bloomsky.onmicrosoft.com",
                    DisplayName = "bebright",
                    Role = "Person"
                }
            },
            AssociatedManagers =
            {
                new ProjectAgentContextPerson
                {
                    Email = "aebright@bloomsky.onmicrosoft.com",
                    DisplayName = "aebright",
                    Role = "Manager"
                }
            }
        };
        var tenantDisplayNames = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["BEBRIGHT@BLOOMSKY.ONMICROSOFT.COM"] = "Bradford Ebright",
            ["aebright@bloomsky.onmicrosoft.com"] = "Alex Ebright"
        };

        DBUtil.EnrichAgentContextDisplayNames(project, tenantDisplayNames);

        var person = Assert.Single(project.AssociatedPeople);
        Assert.Equal("Bradford Ebright", person.DisplayName);
        Assert.Equal("bebright", person.Aliases);
        Assert.Equal("Alex Ebright", Assert.Single(project.AssociatedManagers).DisplayName);
    }
}
