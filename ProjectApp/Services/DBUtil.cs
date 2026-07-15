using System;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using System.Linq;
using System.Collections.Generic;
using Taslow.Project.Model;
using Taslow.Shared.Model;
using Taslow.Project.DAL.Interface;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace Taslow.Project.DAL
{
    public class DBUtil : IProjectDBUtil
    {
        private readonly IConfiguration _configuration;
        private CosmosClient? cosmosClient;
        private static Container? container;

        private const string DatabaseName = "bloomskyHealth";
        private const string ContainerName = "Project";

        public DBUtil(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private Container Container
        {
            get
            {
                if (container != null)
                    return container;

                var connectionString = _configuration["CosmosDBConnection"];

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new InvalidOperationException(
                        "CosmosDBConnection setting is missing");
                }

                cosmosClient = new CosmosClient(connectionString);
                container = cosmosClient.GetContainer(DatabaseName, ContainerName);

                return container;
            }
        }


        private ProjectPersonDTO MapToDTO(AssociatedPeople person,string role)
        {
            return new ProjectPersonDTO
            {
                AssociatedPersonId = person.associatedpersonid,
                PersonName = person.personname,
                PersonAliases = person.personaliases,
                PersonEmail = person.personemail,
                Role = person.role
            };
        }

        public async Task<bool> InsertProject(TaskProject item)
        {
            //item.Id ??= Guid.NewGuid().ToString();
            ItemResponse<TaskProject> response = await Container.CreateItemAsync(item, new PartitionKey(item.tenantid));
            return response.StatusCode == System.Net.HttpStatusCode.OK;
        }


        public async Task<Dictionary<string, TaskProject>> GetProjectDatabyProjectIDList(List<string> projectIds, string tenantid)
        {
           //return project data based on a list of project ids being input
            var projectLookup = new Dictionary<string, TaskProject>();
            foreach (var pid in projectIds)
            {
                try
                {
                    ItemResponse<TaskProject> projectResponse = await Container.ReadItemAsync<TaskProject>(
                        pid,
                        new PartitionKey(tenantid)
                    );
                    projectLookup[pid] = projectResponse.Resource;
                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Project not found, skip
                }

            }
            return projectLookup;

        }

        public async Task<List<string>> GetProjectIdsForManagerAsync(string userEmail, string tenantid)
        {
            var query = new QueryDefinition(
                "SELECT p.id AS ProjectID FROM p JOIN m IN p.AssociatedManagers WHERE m.personEmail = @email and p.tenantID = @tenantID"
            )
                .WithParameter("@email", userEmail)
                .WithParameter("@tenantID", tenantid);

            var results = new List<string>();
            var requestOptions = new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(tenantid) 
            };

            using (var iterator = Container.GetItemQueryIterator<dynamic>(query, requestOptions: requestOptions))
            {
                while (iterator.HasMoreResults)
                {
                    foreach (var item in await iterator.ReadNextAsync())
                    {
                        results.Add(item.ProjectID.ToString());
                    }
                }
            }
            return results;
        }

        internal const string ActiveProjectsByTenantQuery = @"
            SELECT *
            FROM c
            WHERE (c.TenantID = @tenantId
                OR c.tenantID = @tenantId
                OR c.tenantId = @tenantId
                OR c.tenantid = @tenantId)
              AND (LOWER(c.ProjectStatus) = 'active'
                OR LOWER(c.projectStatus) = 'active'
                OR LOWER(c.status) = 'active')
        ";

        public async Task<List<ProjectDTO>> GetActiveProjectsByTenantAsync(string tenantId)
        {
            var query = new QueryDefinition(ActiveProjectsByTenantQuery)
                .WithParameter("@tenantId", tenantId);

            var results = new List<ProjectDTO>();

            using FeedIterator<JObject> iterator =
                Container.GetItemQueryIterator<JObject>(
                    query,
                    requestOptions: new QueryRequestOptions
                    {
                        PartitionKey = new PartitionKey(tenantId)
                    });

            while (iterator.HasMoreResults)
            {
                FeedResponse<JObject> response = await iterator.ReadNextAsync();
                results.AddRange(response.Select(MapActiveProject));
            }

            return results
                .OrderBy(project => project.ProjectName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public  async Task<object> GetProjectAssociationsAsync(
        string tenantId,
        string projectId,
        string mode,
        string role)
        {
            var response = await Container.ReadItemAsync<TaskProject>(
            id: projectId,
            partitionKey: new PartitionKey(tenantId));

            var project = response.Resource;

            var people = (project.associatedpeople ?? new List<AssociatedPeople>())
                .Select(p => MapToDTO(p, "Person"));

            var managers = (project.associatedmanagers ?? new List<AssociatedPeople>())
                .Select(m => MapToDTO(m, "Manager"));

            // ROLE FILTER
            if (role == "people")
                return new { role, people = people.ToList() };

            if (role == "managers")
                return new { role, people = managers.ToList() };

            // MODE SWITCH
            if (mode == "merged")
            {
                return new
                {
                    mode,
                    people = people.Concat(managers)
                                    .GroupBy(p => p.AssociatedPersonId)
                                    .Select(g => g.First())
                                    .ToList()
                };
            }

            // DEFAULT: SEPARATE
            return new
            {
                mode = "separate",
                associatedPeople = people.ToList(),
                associatedManagers = managers.ToList()
            };
        }

        public async Task<Dictionary<string, ProjectDTO>> GetProjectsByIdListAsync(List<string> projectIds, string tenantId)
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE ARRAY_CONTAINS(@ids, c.projectid)")
                .WithParameter("@ids", projectIds);

            var results = new Dictionary<string, ProjectDTO>();

            using var iterator = Container.GetItemQueryIterator<ProjectDTO>(
                query,
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(tenantId)
                });

            while (iterator.HasMoreResults)
            {
                foreach (var project in await iterator.ReadNextAsync())
                {
                    results[project.Id] = project;
                }
            }

            return results;

        }

        public async Task<ProjectAgentContextResponse> GetProjectAgentContextBatchAsync(ProjectAgentContextRequest request)
        {
            var response = new ProjectAgentContextResponse
            {
                TenantId = request.TenantId
            };

            var distinctProjectIds = request.ProjectIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!distinctProjectIds.Any())
            {
                return response;
            }

            var query = new QueryDefinition(
                "SELECT * FROM c WHERE ARRAY_CONTAINS(@ids, c.id) AND (c.TenantID = @tenantId OR c.tenantID = @tenantId OR c.tenantid = @tenantId)")
                .WithParameter("@ids", distinctProjectIds)
                .WithParameter("@tenantId", request.TenantId);

            using var iterator = Container.GetItemQueryIterator<JObject>(
                query,
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(request.TenantId)
                });

            while (iterator.HasMoreResults)
            {
                foreach (var project in await iterator.ReadNextAsync())
                {
                    response.Projects.Add(MapAgentContextProject(project, request));
                }
            }

            return response;
        }

        public async Task<bool> UpdateProjectClientDomainsAsync(ProjectClientDomainsPatchRequest request)
        {
            var normalizedDomains = NormalizeClientDomains(request.ClientDomains);
            var operations = new List<PatchOperation>
            {
                PatchOperation.Set("/clientDomains", normalizedDomains)
            };

            var response = await Container.PatchItemAsync<JObject>(
                id: request.ProjectId,
                partitionKey: new PartitionKey(request.TenantId),
                patchOperations: operations);

            return response.StatusCode == System.Net.HttpStatusCode.OK;
        }

        public async Task<ProjectScopeLinkResponse> LinkProjectScopeGroupTaskSetsAsync(ProjectScopeLinkRequest request)
        {
            var projectResponse = await Container.ReadItemAsync<JObject>(
                id: request.ProjectId,
                partitionKey: new PartitionKey(request.TenantId));

            var project = projectResponse.Resource;
            var scopes = project["ProjectScopes"] as JArray
                ?? project["projectScopes"] as JArray
                ?? project["scopes"] as JArray;

            if (scopes == null)
            {
                return new ProjectScopeLinkResponse
                {
                    TenantId = request.TenantId,
                    ProjectId = request.ProjectId,
                    Updated = false
                };
            }

            var response = new ProjectScopeLinkResponse
            {
                TenantId = request.TenantId,
                ProjectId = request.ProjectId
            };

            foreach (var mapping in request.Mappings ?? new List<ProjectScopeLinkMapping>())
            {
                var scope = scopes
                    .OfType<JObject>()
                    .FirstOrDefault(item =>
                        string.Equals(
                            ReadString(item, "ScopeID", "scopeId", "scopeID"),
                            mapping.ScopeId,
                            StringComparison.OrdinalIgnoreCase));

                if (scope == null)
                {
                    response.Mappings.Add(new ProjectScopeLinkResult
                    {
                        ScopeId = mapping.ScopeId,
                        GroupTaskSetId = mapping.GroupTaskSetId,
                        Status = "scope_not_found",
                        OrchestrationRunId = mapping.OrchestrationRunId ?? string.Empty
                    });
                    continue;
                }

                var existing = ReadString(scope, "GroupTaskSetID", "groupTaskSetId");
                var status = string.IsNullOrWhiteSpace(existing)
                    ? "created"
                    : string.Equals(existing, mapping.GroupTaskSetId, StringComparison.OrdinalIgnoreCase)
                        ? "unchanged"
                        : "updated";

                scope["GroupTaskSetID"] = mapping.GroupTaskSetId;
                scope["groupTaskSetId"] = mapping.GroupTaskSetId;
                scope["LastGroupTaskSetLinkedAt"] = DateTime.UtcNow;
                scope["LastGroupTaskSetLinkRunId"] = mapping.OrchestrationRunId ?? string.Empty;

                response.Updated = true;
                response.Mappings.Add(new ProjectScopeLinkResult
                {
                    ScopeId = mapping.ScopeId,
                    GroupTaskSetId = mapping.GroupTaskSetId,
                    Status = status,
                    OrchestrationRunId = mapping.OrchestrationRunId ?? string.Empty
                });
            }

            if (response.Updated)
            {
                await Container.ReplaceItemAsync(
                    item: project,
                    id: request.ProjectId,
                    partitionKey: new PartitionKey(request.TenantId));
            }

            return response;
        }

        private static ProjectAgentContextProject MapAgentContextProject(
            JObject project,
            ProjectAgentContextRequest request)
        {
            return new ProjectAgentContextProject
            {
                ProjectId = ReadString(project, "id", "ProjectID", "projectId"),
                ProjectName = ReadString(project, "ProjectName", "projectName", "projectNames"),
                Description = ReadString(
                    project,
                    "ProjectDescription",
                    "projectDescription",
                    "description"),
                ProjectStatus = ReadString(project, "ProjectStatus", "projectStatus"),
                ClientDomains = MapClientDomains(project["clientDomains"] ?? project["ClientDomains"]),
                AssociatedPeople = request.IncludeAssociatedPeople
                    ? MapPeople(project["AssociatedPeople"], "Person")
                    : new List<ProjectAgentContextPerson>(),
                AssociatedManagers = request.IncludeAssociatedManagers
                    ? MapPeople(project["AssociatedManagers"], "Manager")
                    : new List<ProjectAgentContextPerson>(),
                Scopes = request.IncludeScopes
                    ? MapScopes(project["ProjectScopes"] ?? project["projectScopes"] ?? project["scopes"])
                    : new List<ProjectAgentContextScope>()
            };
        }

        internal static ProjectDTO MapActiveProject(JObject project)
        {
            return new ProjectDTO
            {
                Id = ReadString(project, "id", "ProjectID", "projectId"),
                ProjectName = ReadString(project, "projectName", "ProjectName", "projectNames", "ProjectNames"),
                ProjectDescription = ReadString(project, "projectDescription", "ProjectDescription", "description"),
                ProjectType = ReadString(project, "projectType", "ProjectType"),
                MarketCode = ReadString(project, "marketCode", "MarketCode", "market_code"),
                ProjectStatus = ReadString(project, "projectStatus", "ProjectStatus", "status"),
                TenantId = ReadString(project, "tenantId", "TenantId", "tenantID", "TenantID", "tenantid"),
                ClientDomains = MapClientDomains(project["clientDomains"] ?? project["ClientDomains"]),
                AssociatedManagers = MapProjectPeople(project["AssociatedManagers"] ?? project["associatedManagers"], "Manager"),
                ProjectScopes = MapProjectScopes(project["ProjectScopes"] ?? project["projectScopes"] ?? project["scopes"])
            };
        }

        private static List<ProjectPersonDTO> MapProjectPeople(JToken? peopleToken, string defaultRole)
        {
            if (peopleToken is not JArray people)
            {
                return new List<ProjectPersonDTO>();
            }

            return people
                .OfType<JObject>()
                .Select(person => new ProjectPersonDTO
                {
                    AssociatedPersonId = ReadGuid(person, "AssociatedPersonID", "associatedPersonId", "personId"),
                    PersonName = ReadString(person, "PersonName", "personName", "displayName", "name"),
                    PersonAliases = ReadString(person, "PersonAliases", "personAliases", "aliases"),
                    PersonEmail = ReadString(person, "PersonEmail", "personEmail", "email"),
                    Role = FirstNonEmpty(ReadString(person, "Role", "role"), defaultRole)
                })
                .ToList();
        }

        private static List<ProjectScopeDTO> MapProjectScopes(JToken? scopesToken)
        {
            if (scopesToken is not JArray scopes)
            {
                return new List<ProjectScopeDTO>();
            }

            return scopes
                .OfType<JObject>()
                .Select(scope => new ProjectScopeDTO
                {
                    ScopeId = ReadString(scope, "ScopeID", "scopeId", "scopeID"),
                    ProjectScopeAreaTitle = ReadString(
                        scope,
                        "ProjectScopeAreaTitle",
                        "projectScopeAreaTitle",
                        "scopeTitle",
                        "title"),
                    ProjectScopeArea = ReadString(
                        scope,
                        "ProjectScopeArea",
                        "projectScopeArea",
                        "scopeDescription",
                        "description"),
                    GroupTaskSetId = ReadString(scope, "GroupTaskSetID", "groupTaskSetId")
                })
                .ToList();
        }

        private static Guid ReadGuid(JObject item, params string[] propertyNames)
        {
            var value = ReadString(item, propertyNames);
            return Guid.TryParse(value, out var parsed) ? parsed : Guid.Empty;
        }

        private static string FirstNonEmpty(params string[] values)
            => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

        private static List<string> MapClientDomains(JToken? domainsToken)
        {
            if (domainsToken is not JArray domains)
            {
                return new List<string>();
            }

            return domains
                .Select(domain => domain?.ToString()?.Trim().TrimStart('@').ToLowerInvariant())
                .Where(domain => !string.IsNullOrWhiteSpace(domain))
                .Select(domain => domain!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(domain => domain)
                .ToList();
        }

        private static List<string> NormalizeClientDomains(IEnumerable<string> domains)
        {
            if (domains == null)
            {
                return new List<string>();
            }

            return domains
                .Select(domain => domain.Trim().TrimStart('@').ToLowerInvariant())
                .Where(domain => !string.IsNullOrWhiteSpace(domain))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(domain => domain)
                .ToList();
        }

        private static List<ProjectAgentContextPerson> MapPeople(JToken? peopleToken, string defaultRole)
        {
            if (peopleToken is not JArray people)
            {
                return new List<ProjectAgentContextPerson>();
            }

            return people
                .OfType<JObject>()
                .Select(person => new ProjectAgentContextPerson
                {
                    PersonId = ReadString(
                        person,
                        "AssociatedPersonID",
                        "associatedPersonId",
                        "personId"),
                    Email = ReadString(person, "PersonEmail", "personEmail", "email"),
                    DisplayName = ReadString(person, "PersonName", "personName", "displayName", "name"),
                    Aliases = ReadString(person, "PersonAliases", "personAliases", "aliases"),
                    Role = FirstNonEmpty(ReadString(person, "Role", "role"), defaultRole)
                })
                .ToList();
        }

        private static List<ProjectAgentContextScope> MapScopes(JToken? scopesToken)
        {
            if (scopesToken is not JArray scopes)
            {
                return new List<ProjectAgentContextScope>();
            }

            return scopes
                .OfType<JObject>()
                .Select(scope => new ProjectAgentContextScope
                {
                    ScopeId = ReadString(scope, "ScopeID", "scopeId", "scopeID"),
                    ScopeTitle = ReadString(
                        scope,
                        "ProjectScopeAreaTitle",
                        "scopeTitle",
                        "title"),
                    ScopeDescription = ReadString(
                        scope,
                        "ProjectScopeArea",
                        "scopeDescription",
                        "description"),
                    GroupTaskSetId = ReadString(scope, "GroupTaskSetID", "groupTaskSetId")
                })
                .ToList();
        }

        private static string ReadString(JObject item, params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                var token = item[propertyName];
                if (token != null && token.Type != JTokenType.Null)
                {
                    var value = token.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }

            return string.Empty;
        }

    }
}



