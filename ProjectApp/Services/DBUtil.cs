using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Taslow.Project.DAL.Interface;
using Taslow.Project.Model;
using Taslow.Shared.Model;

namespace Taslow.Project.DAL
{
    public class DBUtil : IProjectDBUtil
    {
        private readonly IConfiguration _configuration;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _container;

        private const string DatabaseName = "bloomskyHealth";
        private const string ContainerName = "Project";

        public DBUtil(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            var connectionString = _configuration["CosmosDBConnection"];
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("CosmosDBConnection setting is missing");
            }

            _cosmosClient = new CosmosClient(connectionString);
            _container = _cosmosClient.GetContainer(DatabaseName, ContainerName);
        }

        private static string NormalizeEmail(string email)
        {
            return (email ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string NormalizeScope(string scopeArea)
        {
            return (scopeArea ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string ResolveTenantPartitionKey(TaskProject project, string fallbackTenantId)
        {
            var fromDocument = project?.tenantid?.Trim();
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

        private static bool SequenceEquals(IReadOnlyList<float> left, IReadOnlyList<float> right)
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
            catch
            {
                return false;
            }
        }

        private static string BuildPersonNameFromEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return string.Empty;
            }

            var local = email.Split('@').FirstOrDefault() ?? string.Empty;
            return local.Replace('.', ' ').Replace('_', ' ').Trim();
        }

        private static ProjectPersonDTO MapToDTO(AssociatedPeople person, string fallbackRole)
        {
            return new ProjectPersonDTO
            {
                AssociatedPersonId = person.associatedpersonid,
                PersonName = person.personname,
                PersonAliases = person.personaliases,
                PersonEmail = person.personemail,
                Role = string.IsNullOrWhiteSpace(person.role) ? fallbackRole : person.role
            };
        }

        private static ProjectDTO MapToProjectDto(TaskProject project)
        {
            if (project == null)
            {
                return null;
            }

            return new ProjectDTO
            {
                Id = project.Id,
                ProjectName = project.ProjectNames,
                ProjectDescription = project.projectdescription,
                ProjectType = project.projecttype,
                ProjectStatus = project.projectstatus,
                TenantId = project.tenantid
            };
        }

        private static ProjectDetailDTO MapToDetailDto(TaskProject project)
        {
            if (project == null)
            {
                return null;
            }

            var associatedPeople = (project.associatedpeople ?? new List<AssociatedPeople>())
                .Select(p => MapToDTO(p, "Person"))
                .ToList();

            var associatedManagers = (project.associatedmanagers ?? new List<AssociatedPeople>())
                .Select(p => MapToDTO(p, "Manager"))
                .ToList();

            var scopes = (project.projectscopes ?? new List<ProjectScope>())
                .Where(scope => !scope.isarchived)
                .Select(scope => new ProjectScopeDTO
                {
                    ScopeId = scope.scopeid,
                    ProjectScopeArea = scope.projectscopearea,
                    ProjectScopeAreaEmbeddings = scope.projectscopeareaembeddings ?? new List<float>()
                })
                .ToList();

            return new ProjectDetailDTO
            {
                Id = project.Id,
                ProjectName = project.ProjectNames,
                ProjectDescription = project.projectdescription,
                ProjectType = project.projecttype,
                ProjectStatus = project.projectstatus,
                TenantId = project.tenantid,
                ExtProjectId = project.ExtProjectID,
                AssociatedPeople = associatedPeople,
                AssociatedManagers = associatedManagers,
                Scopes = scopes
            };
        }

        private static ProjectScopeSyncItem MapToSyncItem(ProjectScope scope)
        {
            return new ProjectScopeSyncItem
            {
                ScopeId = scope.scopeid,
                ProjectScopeArea = scope.projectscopearea,
                ProjectScopeAreaEmbeddings = scope.projectscopeareaembeddings ?? new List<float>(),
                GroupTaskSetId = scope.grouptasksetid
            };
        }

        private async Task<TaskProject> ReadProjectAsync(string tenantId, string projectId)
        {
            var response = await _container.ReadItemAsync<TaskProject>(
                id: projectId,
                partitionKey: new PartitionKey(tenantId));

            return response.Resource;
        }

        public async Task<bool> InsertProject(TaskProject item)
        {
            item.tenantid = ResolveTenantPartitionKey(item, item?.tenantid);
            ItemResponse<TaskProject> response = await _container.CreateItemAsync(
                item,
                new PartitionKey(item.tenantid));

            return response.StatusCode == HttpStatusCode.Created ||
                   response.StatusCode == HttpStatusCode.OK;
        }

        public async Task<Dictionary<string, TaskProject>> GetProjectDatabyProjectIDList(List<string> projectIds, string tenantid)
        {
            var projectLookup = new Dictionary<string, TaskProject>();

            foreach (var pid in projectIds)
            {
                try
                {
                    ItemResponse<TaskProject> projectResponse = await _container.ReadItemAsync<TaskProject>(
                        pid,
                        new PartitionKey(tenantid));

                    projectLookup[pid] = projectResponse.Resource;
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    // Skip unknown ids.
                }
            }

            return projectLookup;
        }

        public async Task<List<string>> GetProjectIdsForManagerAsync(string userEmail, string tenantid)
        {
            var normalizedEmail = NormalizeEmail(userEmail);
            var query = new QueryDefinition(
                "SELECT p.id AS ProjectID FROM p JOIN m IN p.AssociatedManagers " +
                "WHERE p.TenantID = @tenantId AND (LOWER(m.PersonEmail) = @email OR LOWER(m.personEmail) = @email)")
                .WithParameter("@email", normalizedEmail)
                .WithParameter("@tenantId", tenantid);

            var results = new List<string>();
            var requestOptions = new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(tenantid)
            };

            using (var iterator = _container.GetItemQueryIterator<dynamic>(query, requestOptions: requestOptions))
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

        public async Task<List<TaskProject>> GetActiveProjectsByTenantAsync(string tenantId)
        {
            var query = new QueryDefinition(@"
                SELECT c.id, c.ExtProjectID, c.ProjectName, c.ProjectDescription, c.ProjectType, c.ProjectStatus, c.tenantID
                FROM c
                WHERE c.tenantID = @tenantId
                  AND c.ProjectStatus = 'Active'
                ORDER BY c.ProjectName")
                .WithParameter("@tenantId", tenantId);

            var results = new List<TaskProject>();

            using FeedIterator<TaskProject> iterator = _container.GetItemQueryIterator<TaskProject>(
                query,
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(tenantId)
                });

            while (iterator.HasMoreResults)
            {
                FeedResponse<TaskProject> response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }

        public async Task<object> GetProjectAssociationsAsync(
            string tenantId,
            string projectId,
            string mode,
            string role)
        {
            var project = await ReadProjectAsync(tenantId, projectId);

            var people = (project.associatedpeople ?? new List<AssociatedPeople>())
                .Select(p => MapToDTO(p, "Person"));

            var managers = (project.associatedmanagers ?? new List<AssociatedPeople>())
                .Select(m => MapToDTO(m, "Manager"));

            if (role == "people")
            {
                return new { role, people = people.ToList() };
            }

            if (role == "managers")
            {
                return new { role, people = managers.ToList() };
            }

            if (mode == "merged")
            {
                return new
                {
                    mode,
                    people = people
                        .Concat(managers)
                        .GroupBy(p => NormalizeEmail(p.PersonEmail))
                        .Select(group => group.First())
                        .ToList()
                };
            }

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
                "SELECT * FROM c WHERE ARRAY_CONTAINS(@ids, c.id)")
                .WithParameter("@ids", projectIds);

            var results = new Dictionary<string, ProjectDTO>();

            using var iterator = _container.GetItemQueryIterator<TaskProject>(
                query,
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(tenantId)
                });

            while (iterator.HasMoreResults)
            {
                foreach (var project in await iterator.ReadNextAsync())
                {
                    var dto = MapToProjectDto(project);
                    if (dto != null && !string.IsNullOrWhiteSpace(dto.Id))
                    {
                        results[dto.Id] = dto;
                    }
                }
            }

            return results;
        }

        public async Task<ProjectDetailDTO> GetProjectDetailAsync(string tenantId, string projectId)
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
                    .Any(p => NormalizeEmail(p.personemail) == normalizedManager);
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
            if (request == null)
            {
                throw new ArgumentException("Metadata patch payload is required.");
            }

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
            await _container.ReplaceItemAsync(project, project.Id, new PartitionKey(partitionKey));

            return MapToDetailDto(project);
        }

        public async Task<ProjectDetailDTO> PatchProjectAssociationsAsync(
            string tenantId,
            string projectId,
            ProjectAssociationPatchRequest request)
        {
            if (request == null)
            {
                throw new ArgumentException("Association patch payload is required.");
            }

            var project = await ReadProjectAsync(tenantId, projectId);

            var members = (request.Members ?? new List<string>())
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Select(email => email.Trim())
                .ToList();

            var managers = (request.Managers ?? new List<string>())
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
                throw new InvalidOperationException(
                    $"Invalid email values: {string.Join(", ", invalidEmails)}");
            }

            var normalizedIncoming = members
                .Concat(managers)
                .Select(NormalizeEmail)
                .ToList();

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
                .ToHashSet();

            var alreadyAssociated = normalizedIncoming
                .Where(existingEmails.Contains)
                .Distinct()
                .ToList();

            if (alreadyAssociated.Any())
            {
                throw new InvalidOperationException(
                    $"Emails already associated to this project: {string.Join(", ", alreadyAssociated)}");
            }

            project.associatedpeople ??= new List<AssociatedPeople>();
            project.associatedmanagers ??= new List<AssociatedPeople>();

            foreach (var member in members)
            {
                project.associatedpeople.Add(new AssociatedPeople
                {
                    associatedpersonid = Guid.NewGuid(),
                    personemail = member,
                    personname = BuildPersonNameFromEmail(member),
                    role = "Person"
                });
            }

            foreach (var manager in managers)
            {
                project.associatedmanagers.Add(new AssociatedPeople
                {
                    associatedpersonid = Guid.NewGuid(),
                    personemail = manager,
                    personname = BuildPersonNameFromEmail(manager),
                    role = "Manager"
                });
            }

            project.lastmodifieddate = DateTime.UtcNow;
            var partitionKey = ResolveTenantPartitionKey(project, tenantId);
            project.tenantid = partitionKey;
            await _container.ReplaceItemAsync(project, project.Id, new PartitionKey(partitionKey));

            return MapToDetailDto(project);
        }

        public async Task<ProjectScopePatchResultDTO> PatchProjectScopesAsync(
            string tenantId,
            string projectId,
            ProjectScopePatchRequest request)
        {
            if (request == null)
            {
                throw new ArgumentException("Scope patch payload is required.");
            }

            var project = await ReadProjectAsync(tenantId, projectId);
            var existingScopes = project.projectscopes ?? new List<ProjectScope>();
            var incomingScopes = request.Scopes ?? new List<ProjectScopePatchItem>();

            var sanitizedIncoming = incomingScopes
                .Select(scope => new ProjectScopePatchItem
                {
                    ScopeId = scope.ScopeId?.Trim(),
                    ProjectScopeArea = scope.ProjectScopeArea?.Trim(),
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
                .ToDictionary(scope => scope.scopeid, scope => scope);

            var payload = new ProjectScopeSyncPayload
            {
                TenantId = tenantId,
                ProjectId = projectId,
                GeneratedAtUtc = DateTime.UtcNow
            };

            var retainedScopeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var incoming in sanitizedIncoming)
            {
                ProjectScope targetScope = null;

                if (!string.IsNullOrWhiteSpace(incoming.ScopeId) &&
                    existingById.TryGetValue(incoming.ScopeId, out var existingByScopeId))
                {
                    targetScope = existingByScopeId;
                }
                else
                {
                    targetScope = existingScopes.FirstOrDefault(scope =>
                        !scope.isarchived &&
                        NormalizeScope(scope.projectscopearea) == NormalizeScope(incoming.ProjectScopeArea) &&
                        !retainedScopeIds.Contains(scope.scopeid));
                }

                if (targetScope == null)
                {
                    targetScope = new ProjectScope
                    {
                        scopeid = Guid.NewGuid().ToString(),
                        projectscopearea = incoming.ProjectScopeArea,
                        projectscopeareaembeddings = incoming.ProjectScopeAreaEmbeddings,
                        grouptasksetid = null,
                        isarchived = false
                    };

                    existingScopes.Add(targetScope);
                    payload.Added.Add(MapToSyncItem(targetScope));
                }
                else
                {
                    var areaChanged = !string.Equals(
                        targetScope.projectscopearea,
                        incoming.ProjectScopeArea,
                        StringComparison.Ordinal);

                    var embeddingsChanged = !SequenceEquals(
                        targetScope.projectscopeareaembeddings,
                        incoming.ProjectScopeAreaEmbeddings);

                    targetScope.projectscopearea = incoming.ProjectScopeArea;
                    targetScope.projectscopeareaembeddings = incoming.ProjectScopeAreaEmbeddings;
                    targetScope.isarchived = false;

                    if (areaChanged || embeddingsChanged)
                    {
                        payload.Updated.Add(MapToSyncItem(targetScope));
                    }
                }

                retainedScopeIds.Add(targetScope.scopeid);
            }

            foreach (var existing in existingScopes.Where(scope =>
                !scope.isarchived &&
                !retainedScopeIds.Contains(scope.scopeid)).ToList())
            {
                existing.isarchived = true;
                payload.Removed.Add(MapToSyncItem(existing));
            }

            project.projectscopes = existingScopes;
            project.lastmodifieddate = DateTime.UtcNow;
            var partitionKey = ResolveTenantPartitionKey(project, tenantId);
            project.tenantid = partitionKey;
            await _container.ReplaceItemAsync(project, project.Id, new PartitionKey(partitionKey));

            return new ProjectScopePatchResultDTO
            {
                Project = MapToDetailDto(project),
                ScopeSync = payload
            };
        }
    }
}
