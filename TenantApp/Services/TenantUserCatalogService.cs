using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Taslow.Shared.Model;
using Taslow.Tenant.Service.Interface;

namespace Taslow.Tenant.Service
{
    public class TenantUserCatalogService : ITenantUserCatalogService
    {
        private readonly Container _projectContainer;
        private readonly Container _tenantContainer;
        private readonly ILogger<TenantUserCatalogService> _logger;

        public TenantUserCatalogService(
            IConfiguration configuration,
            ILogger<TenantUserCatalogService> logger)
        {
            var connection = configuration["CosmosDBConnection"];
            var databaseName = configuration["ProjectCosmosDatabaseName"]
                ?? configuration["TenantCosmosDatabaseName"]
                ?? "bloomskyHealth";
            var containerName = configuration["ProjectCosmosContainerName"] ?? "Project";
            var tenantDatabaseName = configuration["TenantCosmosDatabaseName"] ?? databaseName;
            var tenantContainerName = configuration["TenantCosmosContainerName"] ?? "Tenant";

            if (string.IsNullOrWhiteSpace(connection))
            {
                throw new InvalidOperationException("CosmosDBConnection setting is missing.");
            }

            var client = new CosmosClient(connection);
            _projectContainer = client.GetContainer(databaseName, containerName);
            _tenantContainer = client.GetContainer(tenantDatabaseName, tenantContainerName);
            _logger = logger;
        }

        public async Task<IReadOnlyList<SelectableUser>> GetSelectableUsersAsync(
            string tenantId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                return Array.Empty<SelectableUser>();
            }

            var users = new Dictionary<string, ProjectPersonAccumulator>(StringComparer.OrdinalIgnoreCase);
            var query = new QueryDefinition(@"
                SELECT c.id, c.ProjectName, c.AssociatedPeople, c.AssociatedManagers
                FROM c
                WHERE (c.tenantID = @tenantId OR c.TenantID = @tenantId OR c.tenantId = @tenantId)
                  AND (NOT IS_DEFINED(c.ProjectStatus) OR c.ProjectStatus = 'Active' OR c.ProjectStatus = 'active')")
                .WithParameter("@tenantId", tenantId);

            using var iterator = _projectContainer.GetItemQueryIterator<JObject>(
                query,
                requestOptions: new QueryRequestOptions
                {
                    MaxItemCount = 100
                });

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                foreach (var project in response)
                {
                    AddPeople(
                        users,
                        GetProperty(project, "AssociatedManagers", "associatedManagers", "associatedmanagers"),
                        isManager: true);
                    AddPeople(
                        users,
                        GetProperty(project, "AssociatedPeople", "associatedPeople", "associatedpeople"),
                        isManager: false);
                }
            }

            await AddExplicitTenantUsersAsync(users, tenantId, cancellationToken);

            return users.Values
                .Select(person => person.ToSelectableUser(tenantId))
                .OrderBy(user => user.DisplayName)
                .ThenBy(user => user.Email)
                .ToList();
        }

        private async Task AddExplicitTenantUsersAsync(
            IDictionary<string, ProjectPersonAccumulator> users,
            string tenantId,
            CancellationToken cancellationToken)
        {
            try
            {
                var response = await _tenantContainer.ReadItemAsync<JObject>(
                    tenantId,
                    new PartitionKey(tenantId),
                    cancellationToken: cancellationToken);
                var entries = GetProperty(response.Resource, "tenant_users", "tenantUsers") as JArray;
                if (entries == null)
                {
                    return;
                }

                foreach (var entry in entries.OfType<JObject>())
                {
                    var isActive = entry.TryGetValue("isActive", StringComparison.OrdinalIgnoreCase, out var activeToken)
                        ? activeToken?.Value<bool>() ?? true
                        : true;
                    if (!isActive)
                    {
                        continue;
                    }

                    var email = NormalizeEmail(ReadString(entry, "email", "Email"));
                    if (string.IsNullOrWhiteSpace(email))
                    {
                        continue;
                    }

                    if (!users.TryGetValue(email, out var person))
                    {
                        person = new ProjectPersonAccumulator { Email = email };
                        users[email] = person;
                    }

                    var displayName = ReadString(entry, "displayName", "DisplayName", "name", "Name");
                    if (!string.IsNullOrWhiteSpace(displayName))
                    {
                        person.DisplayName = displayName;
                    }

                    person.ExplicitUserId = ReadString(entry, "userId", "UserId");
                    person.IsExplicitTenantUser = true;
                    person.LeaderMarketCodes = (GetProperty(entry, "leaderMarketCodes", "leader_market_codes") as JArray)?
                        .Values<string>()
                        .Where(code => !string.IsNullOrWhiteSpace(code))
                        .Select(code => code!.Trim().ToUpperInvariant())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(code => code)
                        .ToList()
                        ?? new List<string>();
                    person.IsLeader = person.LeaderMarketCodes.Count > 0;
                }
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Tenant document {TenantId} was not found while building the user catalog.", tenantId);
            }
        }

        private void AddPeople(
            IDictionary<string, ProjectPersonAccumulator> users,
            JToken? people,
            bool isManager)
        {
            if (people is not JArray entries)
            {
                return;
            }

            foreach (var entry in entries.OfType<JObject>())
            {
                var email = ReadString(entry, "PersonEmail", "personEmail", "personemail", "email", "Email");
                if (string.IsNullOrWhiteSpace(email))
                {
                    continue;
                }

                var normalizedEmail = NormalizeEmail(email);
                if (string.IsNullOrWhiteSpace(normalizedEmail))
                {
                    continue;
                }

                if (!users.TryGetValue(normalizedEmail, out var person))
                {
                    person = new ProjectPersonAccumulator
                    {
                        Email = normalizedEmail,
                        DisplayName = ReadString(entry, "PersonName", "personName", "personname", "name", "Name")
                    };
                    users[normalizedEmail] = person;
                }

                if (string.IsNullOrWhiteSpace(person.DisplayName))
                {
                    person.DisplayName = BuildDisplayName(normalizedEmail);
                }

                if (isManager)
                {
                    person.IsManager = true;
                }
                else
                {
                    person.IsAssociatedPerson = true;
                }
            }
        }

        private static JToken? GetProperty(JObject source, params string[] names)
        {
            foreach (var name in names)
            {
                if (source.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out var value))
                {
                    return value;
                }
            }

            return null;
        }

        private static string ReadString(JObject source, params string[] names)
            => names
                .Select(name => source.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out var value)
                    ? value?.ToString()
                    : null)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?
                .Trim()
                ?? string.Empty;

        private static string NormalizeEmail(string value)
            => value.Trim().ToLowerInvariant();

        private static string BuildDisplayName(string email)
        {
            var local = email.Split('@')[0];
            var parts = local.Split(new[] { '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0
                ? email
                : string.Join(" ", parts.Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        }

        private static string NormalizeSubjectPart(string value)
            => value.Trim().ToLowerInvariant().Replace("@", "_at_").Replace(".", "_");

        private sealed class ProjectPersonAccumulator
        {
            public string Email { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public bool IsManager { get; set; }
            public bool IsAssociatedPerson { get; set; }
            public bool IsExplicitTenantUser { get; set; }
            public bool IsLeader { get; set; }
            public string ExplicitUserId { get; set; } = string.Empty;
            public List<string> LeaderMarketCodes { get; set; } = new();

            public SelectableUser ToSelectableUser(string tenantId)
            {
                var roles = new List<string>();
                if (IsManager)
                {
                    roles.Add(TenantRoles.TenantPm);
                }

                if (IsLeader)
                {
                    roles.Add(TenantRoles.TenantLeader);
                }

                if (roles.Count == 0)
                {
                    roles.Add(TenantRoles.TenantUser);
                }

                var role = IsManager
                    ? TenantRoles.TenantPm
                    : IsLeader
                        ? TenantRoles.TenantLeader
                        : TenantRoles.TenantUser;
                var subject = $"synthetic:{tenantId}:{NormalizeSubjectPart(Email)}";
                var sources = new List<string>();
                if (IsManager)
                {
                    sources.Add("project_manager");
                }

                if (IsAssociatedPerson)
                {
                    sources.Add("project_participant");
                }

                if (IsExplicitTenantUser)
                {
                    sources.Add(IsLeader ? "tenant_leader_assignment" : "tenant_user_assignment");
                }

                return new SelectableUser
                {
                    TenantUserId = subject,
                    Subject = subject,
                    DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? BuildDisplayName(Email) : DisplayName,
                    Email = Email,
                    Source = sources.Count == 0 ? "project_participant" : string.Join(",", sources),
                    Provider = AuthProviders.Synthetic,
                    IdentityMode = IdentityModes.Synthetic,
                    PrimaryRole = role,
                    Roles = roles,
                    LeaderMarketCodes = LeaderMarketCodes,
                    RoleDerivationSummary = IsLeader
                        ? $"Tenant leader for Market Codes: {string.Join(", ", LeaderMarketCodes)}."
                        : IsManager
                            ? "Derived from project manager relationship."
                            : "Derived from project participant relationship."
                };
            }
        }
    }
}
