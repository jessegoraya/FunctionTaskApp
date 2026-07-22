using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Taslow.Shared.Model;
using Taslow.Tenant.Model;
using Taslow.Tenant.Service.Interface;

namespace Taslow.Tenant.Service
{
    public class LogicAppEmailTaskWriteClient : IEmailTaskWriteClient
    {
        private static readonly TokenRequestContext ProjectApiTokenContext = new(
            new[] { "https://management.azure.com/.default" });
        private static readonly Guid EmptyGuid = Guid.Empty;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly TokenCredential _credential;

        public LogicAppEmailTaskWriteClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            TokenCredential credential)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _credential = credential;
        }

        public async Task<int> WriteAsync(
            TenantEmailExtractionQueueMessage message,
            TenantEmailExtractionInvokeResponse extraction,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            if (!extraction.Status.Equals("tasks_ready", StringComparison.OrdinalIgnoreCase)
                || extraction.Tasks.Count == 0)
            {
                return 0;
            }

            var projectIds = extraction.Tasks
                .Select(task => task.ProjectId)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var projects = await ReadProjectContextAsync(
                message.TenantId,
                projectIds,
                correlationId,
                cancellationToken);
            var logicAppEndpoint = GetLogicAppEndpoint();
            var minimumConfidence = ReadMinimumConfidence();
            var written = 0;

            foreach (var task in extraction.Tasks)
            {
                if (task.NeedsReview || task.OverallConfidence < minimumConfidence)
                {
                    continue;
                }

                if (!projects.TryGetValue(task.ProjectId, out var project))
                {
                    throw new TenantEmailIngestionException(
                        "The selected Project could not be hydrated for task writing.",
                        isTransient: false);
                }

                var scope = project.Scopes.SingleOrDefault(item =>
                    item.ScopeId.Equals(task.ScopeId, StringComparison.OrdinalIgnoreCase));
                if (scope == null || string.IsNullOrWhiteSpace(scope.GroupTaskSetId))
                {
                    throw new TenantEmailIngestionException(
                        "The selected Project Scope is not linked to a Group Task Set.",
                        isTransient: false);
                }

                if (!project.People.Contains(task.AssigneeEmail))
                {
                    throw new TenantEmailIngestionException(
                        "The extracted assignee is not a member of the selected Project.",
                        isTransient: false);
                }

                var payload = BuildLogicAppPayload(message, extraction, task, project, scope);
                var groupTaskId = CreateStableGuid(
                    BuildTaskIdempotencyKey(message, task),
                    "group-task");
                if (await TaskAlreadyExistsAsync(
                    message.TenantId,
                    scope.GroupTaskSetId,
                    groupTaskId,
                    correlationId,
                    cancellationToken))
                {
                    continue;
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, logicAppEndpoint)
                {
                    Content = new StringContent(
                        JsonConvert.SerializeObject(payload),
                        Encoding.UTF8,
                        "application/json")
                };
                request.Headers.TryAddWithoutValidation("x-taslow-tenant-id", message.TenantId);
                request.Headers.TryAddWithoutValidation("x-taslow-correlation-id", correlationId);
                request.Headers.TryAddWithoutValidation("x-taslow-agent-run-id", extraction.AgentRunId);
                request.Headers.TryAddWithoutValidation(
                    "x-taslow-idempotency-key",
                    BuildTaskIdempotencyKey(message, task));

                var client = _httpClientFactory.CreateClient(nameof(LogicAppEmailTaskWriteClient));
                HttpResponseMessage response;
                try
                {
                    response = await client.SendAsync(request, cancellationToken);
                }
                catch (HttpRequestException ex)
                {
                    throw new TenantEmailIngestionException(
                        "Logic App task write failed.",
                        isTransient: true,
                        ex);
                }

                using (response)
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new TenantEmailIngestionException(
                            $"Logic App task write failed with status {(int)response.StatusCode}.",
                            IsTransient(response.StatusCode));
                    }
                }

                written++;
            }

            return written;
        }

        private async Task<bool> TaskAlreadyExistsAsync(
            string tenantId,
            string groupTaskSetId,
            Guid groupTaskId,
            string correlationId,
            CancellationToken cancellationToken)
        {
            var baseEndpoint = GetApimEndpoint("TaskServiceEndpoint", "Task service");
            var endpoint = new Uri(
                $"{baseEndpoint.AbsoluteUri.TrimEnd('/')}/grouptaskset/{Uri.EscapeDataString(groupTaskSetId)}/{Uri.EscapeDataString(tenantId)}");
            var token = await _credential.GetTokenAsync(ProjectApiTokenContext, cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            request.Headers.TryAddWithoutValidation("x-correlation-id", correlationId);
            var client = _httpClientFactory.CreateClient(nameof(LogicAppEmailTaskWriteClient));
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new TenantEmailIngestionException(
                    $"Task idempotency preflight failed with status {(int)response.StatusCode}.",
                    IsTransient(response.StatusCode));
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var payload = JObject.Parse(content);
            var tasks = payload["GroupTask"] as JArray
                ?? payload["grouptask"] as JArray
                ?? new JArray();
            return tasks.OfType<JObject>().Any(task =>
                Guid.TryParse(task.Value<string>("GroupTaskID"), out var existingId)
                && existingId == groupTaskId);
        }

        private async Task<Dictionary<string, ProjectWriteContext>> ReadProjectContextAsync(
            string tenantId,
            List<string> projectIds,
            string correlationId,
            CancellationToken cancellationToken)
        {
            var parsed = GetApimEndpoint("ProjectServiceEndpoint", "Project service");
            var endpoint = new Uri($"{parsed.AbsoluteUri.TrimEnd('/')}/internal/projects/agent-context/batch");
            var token = await _credential.GetTokenAsync(ProjectApiTokenContext, cancellationToken);
            var requestBody = new
            {
                tenantId,
                projectIds,
                includeScopes = true,
                includeAssociatedPeople = true,
                includeAssociatedManagers = true
            };
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(
                    JsonConvert.SerializeObject(requestBody),
                    Encoding.UTF8,
                    "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            request.Headers.TryAddWithoutValidation("x-correlation-id", correlationId);

            var client = _httpClientFactory.CreateClient(nameof(LogicAppEmailTaskWriteClient));
            using var response = await client.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new TenantEmailIngestionException(
                    $"Project context hydration failed with status {(int)response.StatusCode}.",
                    IsTransient(response.StatusCode));
            }

            var root = JObject.Parse(content);
            var rows = root["projects"] as JArray ?? new JArray();
            return rows
                .OfType<JObject>()
                .Select(MapProject)
                .Where(project => !string.IsNullOrWhiteSpace(project.ProjectId))
                .ToDictionary(project => project.ProjectId, StringComparer.OrdinalIgnoreCase);
        }

        private Uri GetApimEndpoint(string settingName, string displayName)
        {
            var value = _configuration[$"TenantEmailIngestion:{settingName}"]
                ?? _configuration[$"TenantEmailIngestion__{settingName}"];
            if (!Uri.TryCreate(value, UriKind.Absolute, out var endpoint)
                || endpoint.Scheme != Uri.UriSchemeHttps
                || !endpoint.Host.EndsWith(".azure-api.net", StringComparison.OrdinalIgnoreCase))
            {
                throw new TenantEmailIngestionException(
                    $"The {displayName} APIM endpoint is missing or invalid.",
                    isTransient: false);
            }

            return endpoint;
        }

        private Uri GetLogicAppEndpoint()
        {
            var value = _configuration["TenantEmailIngestion:LogicAppEndpoint"]
                ?? _configuration["TenantEmailIngestion__LogicAppEndpoint"];
            if (!Uri.TryCreate(value, UriKind.Absolute, out var endpoint)
                || endpoint.Scheme != Uri.UriSchemeHttps
                || !endpoint.Host.EndsWith(".logic.azure.com", StringComparison.OrdinalIgnoreCase))
            {
                throw new TenantEmailIngestionException(
                    "The Logic App endpoint is missing or invalid.",
                    isTransient: false);
            }

            return endpoint;
        }

        private double ReadMinimumConfidence()
        {
            var value = _configuration["TenantEmailIngestion:MinimumTaskWriteConfidence"]
                ?? _configuration["TenantEmailIngestion__MinimumTaskWriteConfidence"];
            return double.TryParse(value, out var parsed) && parsed is >= 0 and <= 1
                ? parsed
                : 0.80;
        }

        private static object BuildLogicAppPayload(
            TenantEmailExtractionQueueMessage message,
            TenantEmailExtractionInvokeResponse extraction,
            TenantExtractedTaskAssignment task,
            ProjectWriteContext project,
            ScopeWriteContext scope)
        {
            var now = DateTime.UtcNow.ToString("O");
            var dueDate = DateTimeOffset.TryParse(task.DueDate, out var due)
                ? due.UtcDateTime.ToString("O")
                : DateTimeOffset.MinValue.UtcDateTime.ToString("O");
            var idempotencyKey = BuildTaskIdempotencyKey(message, task);
            var groupTaskId = CreateStableGuid(idempotencyKey, "group-task");
            var individualTaskSetId = CreateStableGuid(idempotencyKey, "individual-task-set");
            var individualTaskId = CreateStableGuid(idempotencyKey, "individual-task");
            var senderName = message.From?.Name ?? string.Empty;
            var senderEmail = message.From?.Email ?? message.Mailbox;
            var notes = $"sourceSystem=TaslowEmailExtractionAgent; agentRunId={extraction.AgentRunId}; graphEventId={message.GraphEventId}; internetMessageId={message.InternetMessageId}; idempotencyKey={idempotencyKey}";
            var groupTask = new
            {
                _type = "GroupTask",
                GroupTaskID = groupTaskId,
                GroupTaskTitle = Truncate(task.Title, 180),
                GroupTaskDescription = string.IsNullOrWhiteSpace(task.Description) ? task.Title : task.Description,
                GroupTaskStatus = "Open",
                GroupTaskDueDate = new[]
                {
                    new
                    {
                        GroupTaskDueDateSequence = 1,
                        GroupTaskDueDate = dueDate,
                        LastGroupTaskDueDate = dueDate
                    }
                },
                GroupTaskClosedDate = DateTimeOffset.MinValue.UtcDateTime.ToString("O"),
                AssociatedDocuments = Array.Empty<object>(),
                AssociatedLOBItems = Array.Empty<object>(),
                AssoicatedDocuments = Array.Empty<object>(),
                AssoicatedLOBItems = Array.Empty<object>(),
                GroupTaskType = "Email Extracted Task",
                GroupTaskStage = "Awaiting Assignment",
                AssignorStakeholderGroup = new
                {
                    AssignorStakeholderGroupID = CreateStableGuid(senderEmail, "assignor"),
                    AssignorStakeholderGroup = senderName
                },
                AssigneeStakeholderGroup = new[]
                {
                    new
                    {
                        AssigneeStakeholderGroupID = CreateStableGuid(task.AssigneeEmail, "assignee"),
                        AssigneeStakeholderGroup = task.AssigneeName
                    }
                },
                GroupTaskNotes = notes,
                FacilitiationComplete = false,
                FacilitiationPreviouslyComplete = false,
                CancellationSent = false,
                ParentGroupTaskID = EmptyGuid,
                CreatedBy = "TaslowEmailExtractionAgent",
                CreatedDate = now,
                LastModifiedBy = "TaslowEmailExtractionAgent",
                LastModifiedDate = now,
                IndividualTaskSets = new[]
                {
                    new
                    {
                        IndividualTaskSetID = individualTaskSetId,
                        CreatedBy = "TaslowEmailExtractionAgent",
                        CreatedDate = now,
                        IndividualTask = new[]
                        {
                            new
                            {
                                IndividualTaskID = individualTaskId,
                                IndividualTaskStatus = "Open",
                                IndividualTaskTitle = Truncate(task.Title, 180),
                                IndividualTaskType = "Email Extracted Task",
                                IndividualTaskDescription = string.IsNullOrWhiteSpace(task.Description) ? task.Title : task.Description,
                                IndividualTaskNotes = notes,
                                Priority = "Normal",
                                AssignedPerson = task.AssigneeEmail,
                                AssociatedRole = task.AssigneeName,
                                PreviouslySent = false,
                                IndividualTaskAssignedDate = now,
                                IndividualTaskDueDate = dueDate,
                                IndividualTaskCancelledDate = DateTimeOffset.MinValue.UtcDateTime.ToString("O"),
                                IndividualTaskApprovalDecision = string.Empty,
                                IndividualTaskCompletedDate = DateTimeOffset.MinValue.UtcDateTime.ToString("O"),
                                CreatedBy = "TaslowEmailExtractionAgent",
                                CreatedDate = now
                            }
                        }
                    }
                }
            };

            return new
            {
                GroupTask = new[] { groupTask },
                grouptask = new[] { groupTask },
                ProjectID = project.ProjectId,
                TenantID = message.TenantId,
                id = scope.GroupTaskSetId,
                ScopeID = scope.ScopeId,
                ProjectScopeAreaTitle = scope.Title,
                ProjectScopeArea = scope.Description,
                ProjectScopeAreaEmbeddings = Array.Empty<double>(),
                OrchestrationRunId = idempotencyKey
            };
        }

        private static ProjectWriteContext MapProject(JObject row)
        {
            var scopes = (row["scopes"] as JArray ?? row["ProjectScopes"] as JArray ?? new JArray())
                .OfType<JObject>()
                .Select(scope => new ScopeWriteContext
                {
                    ScopeId = scope.Value<string>("scopeId")
                        ?? scope.Value<string>("ScopeID")
                        ?? string.Empty,
                    Title = scope.Value<string>("scopeTitle")
                        ?? scope.Value<string>("ProjectScopeAreaTitle")
                        ?? string.Empty,
                    Description = scope.Value<string>("scopeDescription")
                        ?? scope.Value<string>("ProjectScopeArea")
                        ?? string.Empty,
                    GroupTaskSetId = scope.Value<string>("groupTaskSetId")
                        ?? scope.Value<string>("GroupTaskSetID")
                        ?? string.Empty
                })
                .ToList();
            var people = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var token in new[] { row["associatedPeople"], row["AssociatedPeople"], row["associatedManagers"], row["AssociatedManagers"] })
            {
                foreach (var person in token as JArray ?? new JArray())
                {
                    var email = person.Value<string>("email") ?? person.Value<string>("PersonEmail");
                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        people.Add(email.Trim());
                    }
                }
            }

            return new ProjectWriteContext
            {
                ProjectId = row.Value<string>("projectId")
                    ?? row.Value<string>("ProjectID")
                    ?? row.Value<string>("id")
                    ?? string.Empty,
                Scopes = scopes,
                People = people
            };
        }

        private static string BuildTaskIdempotencyKey(
            TenantEmailExtractionQueueMessage message,
            TenantExtractedTaskAssignment task)
        {
            var canonical = $"{message.TenantId}|{message.InternetMessageId}|{task.SourceTaskId}|{task.ProjectId}|{task.ScopeId}|{task.AssigneeEmail}".ToLowerInvariant();
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        }

        private static Guid CreateStableGuid(string value, string purpose)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{purpose}|{value}"));
            var guidBytes = bytes.Take(16).ToArray();
            guidBytes[7] = (byte)((guidBytes[7] & 0x0F) | 0x50);
            guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
            return new Guid(guidBytes);
        }

        private static string Truncate(string value, int maximumLength)
        {
            var text = value ?? string.Empty;
            return text.Length <= maximumLength ? text : text[..maximumLength];
        }

        private static bool IsTransient(HttpStatusCode statusCode)
        {
            return statusCode == HttpStatusCode.RequestTimeout
                || statusCode == (HttpStatusCode)429
                || (int)statusCode >= 500;
        }

        private sealed class ProjectWriteContext
        {
            public string ProjectId { get; set; } = string.Empty;
            public List<ScopeWriteContext> Scopes { get; set; } = new();
            public HashSet<string> People { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class ScopeWriteContext
        {
            public string ScopeId { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string GroupTaskSetId { get; set; } = string.Empty;
        }
    }
}
