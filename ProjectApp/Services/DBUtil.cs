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
using System.Net;
using System.Net.Mail;
using Taslow.Shared.Infrastructure;

namespace Taslow.Project.DAL
{
    public class DBUtil : IProjectDBUtil
    {
        private readonly IConfiguration _configuration;
        private CosmosClient? cosmosClient;
        private static Container? container;
        private static Container? tenantContainer;

        private const string DatabaseName = "bloomskyHealth";
        private const string ContainerName = "Project";
        private const string TenantContainerName = "Tenant";

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

                cosmosClient = CosmosClientFactory.Create(key => _configuration[key]);
                var databaseName = _configuration["ProjectCosmosDatabaseName"]
                    ?? _configuration["CosmosDBDatabaseName"]
                    ?? DatabaseName;
                var containerName = _configuration["ProjectCosmosContainerName"] ?? ContainerName;
                container = cosmosClient.GetContainer(databaseName, containerName);

                return container;
            }
        }

        private Container TenantContainer
        {
            get
            {
                if (tenantContainer != null)
                    return tenantContainer;

                cosmosClient ??= CosmosClientFactory.Create(key => _configuration[key]);
                var databaseName = _configuration["ProjectCosmosDatabaseName"]
                    ?? _configuration["CosmosDBDatabaseName"]
                    ?? DatabaseName;
                var containerName = _configuration["TenantCosmosContainerName"]
                    ?? TenantContainerName;
                tenantContainer = cosmosClient.GetContainer(databaseName, containerName);

                return tenantContainer;
            }
        }


        private static ProjectPersonDTO MapToDTO(AssociatedPeople person, string role)
        {
            return new ProjectPersonDTO
            {
                AssociatedPersonId = person.associatedpersonid,
                PersonName = person.personname,
                PersonAliases = person.personaliases,
                PersonEmail = person.personemail,
                Role = string.IsNullOrWhiteSpace(person.role) ? role : person.role
            };
        }

        private static string NormalizeEmail(string email)
            => (email ?? string.Empty).Trim().ToLowerInvariant();

        private static string NormalizeScope(string scopeArea)
            => (scopeArea ?? string.Empty).Trim().ToLowerInvariant();

        private static string ResolveTenantPartitionKey(TaskProject project, string fallbackTenantId)
        {
            var fromDocument = project.tenantid?.Trim();
            if (!string.IsNullOrWhiteSpace(fromDocument))
            {
                return fromDocument;
            }

            var resolved = (fallbackTenantId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(resolved))
            {
                throw new InvalidOperationException("Project tenant partition key is required.");
            }

            return resolved;
        }

        private static bool SequenceEquals(IReadOnlyList<float>? left, IReadOnlyList<float>? right)
        {
            if (left == null && right == null)
            {
                return true;
            }

            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (var i = 0; i < left.Count; i++)
            {
                if (Math.Abs(left[i] - right[i]) > 0.000001f)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            try
            {
                _ = new MailAddress(email);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static string BuildPersonNameFromEmail(string email)
        {
            var local = (email ?? string.Empty).Split('@').FirstOrDefault() ?? string.Empty;
            return local.Replace('.', ' ').Replace('_', ' ').Trim();
        }

        private static ProjectDetailDTO MapToDetailDto(TaskProject project)
        {
            return new ProjectDetailDTO
            {
                Id = project.Id,
                ProjectName = project.ProjectNames,
                ProjectDescription = project.projectdescription,
                ProjectType = project.projecttype,
                MarketCode = project.marketcode,
                ProjectStatus = project.projectstatus,
                TenantId = project.tenantid,
                ExtProjectId = project.ExtProjectID,
                AssociatedPeople = (project.associatedpeople ?? new List<AssociatedPeople>())
                    .Select(person => MapToDTO(person, "Person"))
                    .ToList(),
                AssociatedManagers = (project.associatedmanagers ?? new List<AssociatedPeople>())
                    .Select(person => MapToDTO(person, "Manager"))
                    .ToList(),
                Scopes = (project.projectscopes ?? new List<ProjectScope>())
                    .Where(scope => !scope.isarchived)
                    .Select(scope => new ProjectScopeDTO
                    {
                        ScopeId = scope.scopeid,
                        ProjectScopeAreaTitle = scope.projectscopeareatitle,
                        ProjectScopeArea = scope.projectscopearea,
                        ProjectScopeAreaEmbeddings = scope.projectscopeareaembeddings ?? new List<float>(),
                        GroupTaskSetId = scope.grouptasksetid ?? string.Empty
                    })
                    .ToList()
            };
        }

        private static ProjectScopeSyncItem MapToSyncItem(ProjectScope scope)
        {
            return new ProjectScopeSyncItem
            {
                ScopeId = scope.scopeid,
                ProjectScopeAreaTitle = scope.projectscopeareatitle,
                ProjectScopeArea = scope.projectscopearea,
                ProjectScopeAreaEmbeddings = scope.projectscopeareaembeddings ?? new List<float>(),
                GroupTaskSetId = scope.grouptasksetid ?? string.Empty
            };
        }

        private async Task<TaskProject> ReadProjectAsync(string tenantId, string projectId)
        {
            var response = await Container.ReadItemAsync<TaskProject>(
                id: projectId,
                partitionKey: new PartitionKey(tenantId));

            return response.Resource;
        }

        public async Task<bool> InsertProject(TaskProject item)
        {
            //item.Id ??= Guid.NewGuid().ToString();
            ItemResponse<TaskProject> response = await Container.CreateItemAsync(item, new PartitionKey(item.tenantid));
            return IsSuccessfulWriteStatus(response.StatusCode);
        }

        internal static bool IsSuccessfulWriteStatus(HttpStatusCode statusCode)
            => (int)statusCode is >= 200 and < 300;


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

        internal const string ProjectIdsForManagerQuery = @"
            SELECT p.id AS ProjectID
            FROM p
            JOIN m IN p.AssociatedManagers
            WHERE (m.PersonEmail = @email OR m.personEmail = @email)
              AND (p.TenantID = @tenantID
                OR p.tenantID = @tenantID
                OR p.tenantId = @tenantID
                OR p.tenantid = @tenantID)";

        public async Task<List<string>> GetProjectIdsForManagerAsync(string userEmail, string tenantid)
        {
            var query = new QueryDefinition(ProjectIdsForManagerQuery)
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

        public async Task<bool> IsTenantActiveAsync(string tenantId)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                return false;
            }

            try
            {
                var response = await TenantContainer.ReadItemAsync<TenantDocumentDTO>(
                    tenantId,
                    new PartitionKey(tenantId));
                return string.Equals(
                    response.Resource?.Tenant?.Status,
                    TenantStatuses.Active,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
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

            var tenantDisplayNames = await GetTenantDisplayNamesByEmailAsync(request.TenantId);
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
                    var contextProject = MapAgentContextProject(project, request);
                    EnrichAgentContextDisplayNames(contextProject, tenantDisplayNames);
                    response.Projects.Add(contextProject);
                }
            }

            return response;
        }

        private async Task<IReadOnlyDictionary<string, string>> GetTenantDisplayNamesByEmailAsync(
            string tenantId)
        {
            var response = await TenantContainer.ReadItemAsync<JObject>(
                id: tenantId,
                partitionKey: new PartitionKey(tenantId));
            var users = response.Resource["tenant_users"]
                ?? response.Resource["tenantUsers"];

            if (users is not JArray tenantUsers)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            return tenantUsers
                .OfType<JObject>()
                .Select(user => new
                {
                    Email = NormalizeEmail(ReadString(user, "email", "Email")),
                    DisplayName = ReadString(user, "displayName", "DisplayName", "name", "Name")
                })
                .Where(user =>
                    !string.IsNullOrWhiteSpace(user.Email)
                    && !string.IsNullOrWhiteSpace(user.DisplayName))
                .GroupBy(user => user.Email, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().DisplayName,
                    StringComparer.OrdinalIgnoreCase);
        }

        internal static void EnrichAgentContextDisplayNames(
            ProjectAgentContextProject project,
            IReadOnlyDictionary<string, string> tenantDisplayNames)
        {
            foreach (var person in project.AssociatedPeople.Concat(project.AssociatedManagers))
            {
                var email = NormalizeEmail(person.Email);
                if (tenantDisplayNames.TryGetValue(email, out var displayName)
                    && !string.IsNullOrWhiteSpace(displayName))
                {
                    var existingName = person.DisplayName?.Trim() ?? string.Empty;
                    var existingAliases = person.Aliases ?? string.Empty;
                    person.DisplayName = displayName.Trim();
                    if (!string.IsNullOrWhiteSpace(existingName)
                        && !string.Equals(existingName, person.DisplayName, StringComparison.OrdinalIgnoreCase)
                        && !existingAliases.Split(
                                ',',
                                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Contains(existingName, StringComparer.OrdinalIgnoreCase))
                    {
                        person.Aliases = string.IsNullOrWhiteSpace(existingAliases)
                            ? existingName
                            : $"{existingAliases},{existingName}";
                    }
                }
            }
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

        public async Task<ProjectDetailDTO?> GetProjectDetailAsync(string tenantId, string projectId)
        {
            try
            {
                var project = await ReadProjectAsync(tenantId, projectId);
                return MapToDetailDto(project);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<bool> IsManagerForProjectAsync(string tenantId, string projectId, string managerEmail)
        {
            if (string.IsNullOrWhiteSpace(managerEmail))
            {
                return false;
            }

            try
            {
                var project = await ReadProjectAsync(tenantId, projectId);
                var normalizedManager = NormalizeEmail(managerEmail);

                return (project.associatedmanagers ?? new List<AssociatedPeople>())
                    .Any(person => NormalizeEmail(person.personemail) == normalizedManager);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        public async Task<ProjectDetailDTO> PatchProjectMetadataAsync(
            string tenantId,
            string projectId,
            ProjectMetadataPatchRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var project = await ReadProjectAsync(tenantId, projectId);

            if (request.ProjectName != null)
            {
                project.ProjectNames = request.ProjectName.Trim();
            }

            if (request.ProjectDescription != null)
            {
                project.projectdescription = request.ProjectDescription.Trim();
            }

            if (request.ProjectType != null)
            {
                project.projecttype = request.ProjectType.Trim();
            }

            if (request.MarketCode != null)
            {
                project.marketcode = request.MarketCode.Trim().ToUpperInvariant();
            }

            if (request.ProjectStatus != null)
            {
                project.projectstatus = request.ProjectStatus.Trim();
            }

            if (request.ExtProjectId != null)
            {
                project.ExtProjectID = request.ExtProjectId.Trim();
            }

            project.lastmodifieddate = DateTime.UtcNow;
            var partitionKey = ResolveTenantPartitionKey(project, tenantId);
            project.tenantid = partitionKey;
            await Container.ReplaceItemAsync(project, project.Id, new PartitionKey(partitionKey));

            return MapToDetailDto(project);
        }

        public async Task<ProjectDetailDTO> PatchProjectAssociationsAsync(
            string tenantId,
            string projectId,
            ProjectAssociationPatchRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var project = await ReadProjectAsync(tenantId, projectId);

            var members = request.Members
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Select(email => email.Trim())
                .ToList();
            var managers = request.Managers
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Select(email => email.Trim())
                .ToList();

            var invalidEmails = members
                .Concat(managers)
                .Where(email => !IsValidEmail(email))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (invalidEmails.Any())
            {
                throw new InvalidOperationException($"Invalid email values: {string.Join(", ", invalidEmails)}");
            }

            var normalizedIncoming = members.Concat(managers).Select(NormalizeEmail).ToList();
            var requestDuplicates = normalizedIncoming
                .GroupBy(email => email)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();
            if (requestDuplicates.Any())
            {
                throw new InvalidOperationException(
                    $"Duplicate emails in request: {string.Join(", ", requestDuplicates)}");
            }

            var existingEmails = (project.associatedpeople ?? new List<AssociatedPeople>())
                .Select(person => NormalizeEmail(person.personemail))
                .Concat((project.associatedmanagers ?? new List<AssociatedPeople>())
                    .Select(person => NormalizeEmail(person.personemail)))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var alreadyAssociated = normalizedIncoming
                .Where(existingEmails.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (alreadyAssociated.Any())
            {
                throw new InvalidOperationException(
                    $"Emails already associated to this project: {string.Join(", ", alreadyAssociated)}");
            }

            project.associatedpeople ??= new List<AssociatedPeople>();
            project.associatedmanagers ??= new List<AssociatedPeople>();
            project.associatedpeople.AddRange(members.Select(member => new AssociatedPeople
            {
                associatedpersonid = Guid.NewGuid(),
                personemail = member,
                personname = BuildPersonNameFromEmail(member),
                role = "Person"
            }));
            project.associatedmanagers.AddRange(managers.Select(manager => new AssociatedPeople
            {
                associatedpersonid = Guid.NewGuid(),
                personemail = manager,
                personname = BuildPersonNameFromEmail(manager),
                role = "Manager"
            }));

            project.lastmodifieddate = DateTime.UtcNow;
            var partitionKey = ResolveTenantPartitionKey(project, tenantId);
            project.tenantid = partitionKey;
            await Container.ReplaceItemAsync(project, project.Id, new PartitionKey(partitionKey));

            return MapToDetailDto(project);
        }

        public async Task<ProjectScopePatchResultDTO> PatchProjectScopesAsync(
            string tenantId,
            string projectId,
            ProjectScopePatchRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var project = await ReadProjectAsync(tenantId, projectId);
            var existingScopes = project.projectscopes ?? new List<ProjectScope>();
            var sanitizedIncoming = request.Scopes
                .Select(scope => new ProjectScopePatchItem
                {
                    ScopeId = scope.ScopeId.Trim(),
                    ProjectScopeAreaTitle = scope.ProjectScopeAreaTitle.Trim(),
                    ProjectScopeArea = scope.ProjectScopeArea.Trim(),
                    ProjectScopeAreaEmbeddings = scope.ProjectScopeAreaEmbeddings ?? new List<float>()
                })
                .ToList();

            if (sanitizedIncoming.Any(scope => string.IsNullOrWhiteSpace(scope.ProjectScopeArea)))
            {
                throw new InvalidOperationException("Each scope row requires a non-empty projectScopeArea value.");
            }

            var duplicateScopes = sanitizedIncoming
                .GroupBy(scope => NormalizeScope(scope.ProjectScopeArea))
                .Where(group => group.Count() > 1)
                .Select(group => group.First().ProjectScopeArea)
                .ToList();
            if (duplicateScopes.Any())
            {
                throw new InvalidOperationException(
                    $"Duplicate scopes in request: {string.Join(", ", duplicateScopes)}");
            }

            var existingById = existingScopes
                .Where(scope => !string.IsNullOrWhiteSpace(scope.scopeid))
                .ToDictionary(scope => scope.scopeid, scope => scope, StringComparer.OrdinalIgnoreCase);
            var payload = new ProjectScopeSyncPayload
            {
                TenantId = tenantId,
                ProjectId = projectId,
                GeneratedAtUtc = DateTime.UtcNow
            };
            var retainedScopeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var incoming in sanitizedIncoming)
            {
                ProjectScope? targetScope = null;
                if (!string.IsNullOrWhiteSpace(incoming.ScopeId)
                    && existingById.TryGetValue(incoming.ScopeId, out var scopeById))
                {
                    targetScope = scopeById;
                }
                else
                {
                    targetScope = existingScopes.FirstOrDefault(scope =>
                        !scope.isarchived
                        && NormalizeScope(scope.projectscopearea) == NormalizeScope(incoming.ProjectScopeArea)
                        && !retainedScopeIds.Contains(scope.scopeid));
                }

                if (targetScope == null)
                {
                    targetScope = new ProjectScope
                    {
                        scopeid = Guid.NewGuid().ToString(),
                        projectscopeareatitle = incoming.ProjectScopeAreaTitle,
                        projectscopearea = incoming.ProjectScopeArea,
                        projectscopeareaembeddings = incoming.ProjectScopeAreaEmbeddings,
                        isarchived = false
                    };
                    existingScopes.Add(targetScope);
                    payload.Added.Add(MapToSyncItem(targetScope));
                }
                else
                {
                    var changed = !string.Equals(targetScope.projectscopearea, incoming.ProjectScopeArea, StringComparison.Ordinal)
                        || !string.Equals(targetScope.projectscopeareatitle, incoming.ProjectScopeAreaTitle, StringComparison.Ordinal)
                        || !SequenceEquals(targetScope.projectscopeareaembeddings, incoming.ProjectScopeAreaEmbeddings);
                    targetScope.projectscopeareatitle = incoming.ProjectScopeAreaTitle;
                    targetScope.projectscopearea = incoming.ProjectScopeArea;
                    targetScope.projectscopeareaembeddings = incoming.ProjectScopeAreaEmbeddings;
                    targetScope.isarchived = false;
                    if (changed)
                    {
                        payload.Updated.Add(MapToSyncItem(targetScope));
                    }
                }

                retainedScopeIds.Add(targetScope.scopeid);
            }

            foreach (var existing in existingScopes
                .Where(scope => !scope.isarchived && !retainedScopeIds.Contains(scope.scopeid))
                .ToList())
            {
                existing.isarchived = true;
                payload.Removed.Add(MapToSyncItem(existing));
            }

            project.projectscopes = existingScopes;
            project.lastmodifieddate = DateTime.UtcNow;
            var partitionKey = ResolveTenantPartitionKey(project, tenantId);
            project.tenantid = partitionKey;
            await Container.ReplaceItemAsync(project, project.Id, new PartitionKey(partitionKey));

            return new ProjectScopePatchResultDTO
            {
                Project = MapToDetailDto(project),
                ScopeSync = payload
            };
        }

        public async Task<ProjectScopeGtsLinkResultDTO> LinkScopeGroupTaskSetsAsync(
            string tenantId,
            string projectId,
            ProjectScopeGtsLinkRequest request)
        {
            if (request?.Mappings == null || !request.Mappings.Any())
            {
                throw new ArgumentException("At least one scope-to-GTS mapping is required.");
            }

            var project = await ReadProjectAsync(tenantId, projectId);
            project.projectscopes ??= new List<ProjectScope>();
            var linkedCount = 0;
            var noOpCount = 0;
            var conflicts = new List<string>();

            foreach (var mapping in request.Mappings)
            {
                var scopeId = mapping.ScopeId.Trim();
                var groupTaskSetId = mapping.GroupTaskSetId.Trim();
                if (string.IsNullOrWhiteSpace(scopeId) || string.IsNullOrWhiteSpace(groupTaskSetId))
                {
                    throw new InvalidOperationException("Each mapping requires scopeId and groupTaskSetId.");
                }

                var scope = project.projectscopes.FirstOrDefault(item =>
                    string.Equals(item.scopeid, scopeId, StringComparison.OrdinalIgnoreCase));
                if (scope == null)
                {
                    throw new InvalidOperationException($"ScopeId not found on project: {scopeId}");
                }

                var existingGtsId = scope.grouptasksetid?.Trim();
                if (string.IsNullOrWhiteSpace(existingGtsId))
                {
                    scope.grouptasksetid = groupTaskSetId;
                    linkedCount++;
                }
                else if (string.Equals(existingGtsId, groupTaskSetId, StringComparison.OrdinalIgnoreCase))
                {
                    noOpCount++;
                }
                else
                {
                    conflicts.Add(
                        $"ScopeId {scopeId} already mapped to GroupTaskSetID {existingGtsId}; incoming value {groupTaskSetId} is conflicting.");
                }
            }

            if (conflicts.Any())
            {
                throw new InvalidOperationException($"CONFLICT: {string.Join(" | ", conflicts)}");
            }

            if (linkedCount > 0)
            {
                project.lastmodifieddate = DateTime.UtcNow;
                var partitionKey = ResolveTenantPartitionKey(project, tenantId);
                project.tenantid = partitionKey;
                await Container.ReplaceItemAsync(project, project.Id, new PartitionKey(partitionKey));
            }

            return new ProjectScopeGtsLinkResultDTO
            {
                LinkedCount = linkedCount,
                NoOpCount = noOpCount,
                Project = MapToDetailDto(project)
            };
        }

        internal static ProjectAgentContextProject MapAgentContextProject(
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
                AssociatedPeople = MapProjectPeople(project["AssociatedPeople"] ?? project["associatedPeople"], "Person"),
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
                        "projectScopeAreaTitle",
                        "scopeTitle",
                        "title"),
                    ScopeDescription = ReadString(
                        scope,
                        "ProjectScopeArea",
                        "projectScopeArea",
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



