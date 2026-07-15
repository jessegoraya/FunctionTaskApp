using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Taslow.Project.Function;
using Taslow.Shared.Model;
using Xunit;

namespace ProjectApp.CharacterizationTests;

public class ProjectContractCharacterizationTests
{
    [Fact]
    public void FunctionRoutes_ShouldMatchApprovedBaseline()
    {
        var expectedPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "project-api-contract.json");
        var expected = JsonConvert.DeserializeObject<List<FunctionContract>>(File.ReadAllText(expectedPath))
            ?? new List<FunctionContract>();

        var actual = typeof(ProjectTaskController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Select(ReadFunctionContract)
            .Where(contract => contract != null)
            .Cast<FunctionContract>()
            .OrderBy(contract => contract.FunctionName, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            JsonConvert.SerializeObject(expected.OrderBy(x => x.FunctionName), Formatting.Indented),
            JsonConvert.SerializeObject(actual, Formatting.Indented));
    }

    [Fact]
    public void SharedProjectContracts_ShouldPreserveJsonPropertyNames()
    {
        AssertPropertyNames(
            new ProjectBatchRequest
            {
                TenantId = "tenant-a",
                ProjectIds = new List<string> { "project-a" }
            },
            "ProjectIds",
            "TenantId");

        AssertPropertyNames(
            new ProjectClientDomainsPatchRequest
            {
                TenantId = "tenant-a",
                ProjectId = "project-a",
                ClientDomains = new List<string> { "example.com" }
            },
            "clientDomains",
            "projectId",
            "tenantId");

        AssertPropertyNames(
            new ProjectScopeLinkResponse
            {
                TenantId = "tenant-a",
                ProjectId = "project-a",
                Updated = true
            },
            "mappings",
            "projectId",
            "tenantId",
            "updated");
    }

    private static FunctionContract? ReadFunctionContract(MethodInfo method)
    {
        var functionAttribute = method.GetCustomAttributes(false)
            .FirstOrDefault(attribute =>
                attribute.GetType().Name is "FunctionNameAttribute" or "FunctionAttribute");
        if (functionAttribute == null)
        {
            return null;
        }

        var functionName = ReadProperty(functionAttribute, "Name")?.ToString() ?? method.Name;
        var triggerAttribute = method.GetParameters()
            .SelectMany(parameter => parameter.GetCustomAttributes(false))
            .FirstOrDefault(attribute => attribute.GetType().Name == "HttpTriggerAttribute");
        Assert.NotNull(triggerAttribute);

        var route = ReadProperty(triggerAttribute!, "Route")?.ToString();
        var methods = (ReadProperty(triggerAttribute!, "Methods") as IEnumerable<string>)
            ?.Select(value => value.ToLowerInvariant())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList()
            ?? new List<string>();

        return new FunctionContract
        {
            FunctionName = functionName,
            Route = string.IsNullOrWhiteSpace(route) ? functionName : route,
            AuthorizationLevel = ReadProperty(triggerAttribute!, "AuthLevel")?.ToString() ?? string.Empty,
            Methods = methods
        };
    }

    private static object? ReadProperty(object instance, string propertyName)
        => instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)?.GetValue(instance);

    private static void AssertPropertyNames(object value, params string[] expectedNames)
    {
        var json = JObject.Parse(JsonConvert.SerializeObject(value));
        var actual = json.Properties().Select(property => property.Name).OrderBy(name => name).ToArray();
        Assert.Equal(expectedNames.OrderBy(name => name), actual);
    }

    private sealed class FunctionContract
    {
        [JsonProperty("functionName")]
        public string FunctionName { get; set; } = string.Empty;

        [JsonProperty("route")]
        public string Route { get; set; } = string.Empty;

        [JsonProperty("authorizationLevel")]
        public string AuthorizationLevel { get; set; } = string.Empty;

        [JsonProperty("methods")]
        public List<string> Methods { get; set; } = new();
    }
}
