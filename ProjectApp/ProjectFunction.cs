using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Taslow.Project.DAL.Interface;
using Taslow.Project.Model;
using Taslow.Project.Service.Interface;
using Taslow.Shared.Model;

namespace Taslow.Project.Function
{
    public class ProjectTaskController
    {
        private readonly IProjectDBUtil _projectDb;
        private readonly IProjectScopeSyncPublisher _scopeSyncPublisher;
        private readonly IConfiguration _configuration;

        public ProjectTaskController(
            IProjectDBUtil projectDb,
            IProjectScopeSyncPublisher scopeSyncPublisher,
            IConfiguration configuration)
        {
            _projectDb = projectDb;
            _scopeSyncPublisher = scopeSyncPublisher;
            _configuration = configuration;
        }

        [FunctionName("CreateProject")]
        public async Task<IActionResult> RunCreateProjectAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var tenantId = req.Query["tenant"].ToString();
            return await CreateProjectInternalAsync(req, tenantId, log);
        }

        [FunctionName("CreateProjectV2")]
        public async Task<IActionResult> CreateProjectV2Async(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "projects/{tenantId}")] HttpRequest req,
            string tenantId,
            ILogger log)
        {
            return await CreateProjectInternalAsync(req, tenantId, log);
        }

        private async Task<IActionResult> CreateProjectInternalAsync(
            HttpRequest req,
            string tenantId,
            ILogger log)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var createRequest = ParseCreateRequest(requestBody);

            if (string.IsNullOrWhiteSpace(tenantId))
            {
                tenantId = createRequest.TenantId;
            }

            if (string.IsNullOrWhiteSpace(tenantId))
            {
                return new BadRequestObjectResult("TenantId is required.");
            }

            if (string.IsNullOrWhiteSpace(createRequest.ProjectName))
            {
                return new BadRequestObjectResult("projectName is required.");
            }

            var now = DateTime.UtcNow;
            var projectId = Guid.NewGuid().ToString();
            var project = new TaskProject
            {
                Id = projectId,
                tenantid = tenantId,
                ProjectNames = createRequest.ProjectName.Trim(),
                projectdescription = createRequest.ProjectDescription?.Trim(),
                projecttype = createRequest.ProjectType?.Trim(),
                projectstatus = string.IsNullOrWhiteSpace(createRequest.ProjectStatus)
                    ? "Active"
                    : createRequest.ProjectStatus.Trim(),
                ExtProjectID = createRequest.ExtProjectId?.Trim(),
                associatedpeople = new List<AssociatedPeople>(),
                associatedmanagers = new List<AssociatedPeople>(),
                projectscopes = new List<ProjectScope>(),
                datecreated = now,
                lastmodifieddate = now
            };

            var inserted = await _projectDb.InsertProject(project);
            if (!inserted)
            {
                return new BadRequestObjectResult("Could not create project.");
            }

            try
            {
                var callerManager = GetCallerManagerEmail(req);
                var managers = createRequest.Managers ?? new List<string>();
                if (!string.IsNullOrWhiteSpace(callerManager) &&
                    !managers.Any(email => NormalizeEmail(email) == NormalizeEmail(callerManager)))
                {
                    managers.Add(callerManager);
                }

                if ((createRequest.Members?.Any() ?? false) || managers.Any())
                {
                    await _projectDb.PatchProjectAssociationsAsync(
                        tenantId,
                        projectId,
                        new ProjectAssociationPatchRequest
                        {
                            Members = createRequest.Members ?? new List<string>(),
                            Managers = managers
                        });
                }

                if (createRequest.Scopes?.Any() ?? false)
                {
                    var scopeResult = await _projectDb.PatchProjectScopesAsync(
                        tenantId,
                        projectId,
                        new ProjectScopePatchRequest { Scopes = createRequest.Scopes });

                    await _scopeSyncPublisher.PublishAsync(scopeResult.ScopeSync);
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Post-create patch failed for project {ProjectId}", projectId);
                return new BadRequestObjectResult(ex.Message);
            }

            var detail = await _projectDb.GetProjectDetailAsync(tenantId, projectId);
            return new OkObjectResult(detail);
        }

        [FunctionName("GetProjectDetail")]
        public async Task<IActionResult> GetProjectDetailAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "projects/{tenantId}/{projectId}/detail")]
            HttpRequest req,
            string tenantId,
            string projectId,
            ILogger log)
        {
            var authResult = await EnsureManagerAuthorizationAsync(req, tenantId, projectId);
            if (authResult != null)
            {
                return authResult;
            }

            var detail = await _projectDb.GetProjectDetailAsync(tenantId, projectId);
            if (detail == null)
            {
                return new NotFoundObjectResult("Project not found.");
            }

            return new OkObjectResult(detail);
        }

        [FunctionName("PatchProjectMetadata")]
        public async Task<IActionResult> PatchProjectMetadataAsync(
            [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "projects/{tenantId}/{projectId}/metadata")]
            HttpRequest req,
            string tenantId,
            string projectId,
            ILogger log)
        {
            var authResult = await EnsureManagerAuthorizationAsync(req, tenantId, projectId);
            if (authResult != null)
            {
                return authResult;
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var patchRequest = ParseMetadataPatchRequest(requestBody);

            try
            {
                var result = await _projectDb.PatchProjectMetadataAsync(tenantId, projectId, patchRequest);
                return new OkObjectResult(result);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Patch metadata failed for project {ProjectId}", projectId);
                return new BadRequestObjectResult(ex.Message);
            }
        }

        [FunctionName("PatchProjectAssociations")]
        public async Task<IActionResult> PatchProjectAssociationsAsync(
            [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "projects/{tenantId}/{projectId}/associations")]
            HttpRequest req,
            string tenantId,
            string projectId,
            ILogger log)
        {
            var authResult = await EnsureManagerAuthorizationAsync(req, tenantId, projectId);
            if (authResult != null)
            {
                return authResult;
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var patchRequest = ParseAssociationPatchRequest(requestBody);

            try
            {
                var result = await _projectDb.PatchProjectAssociationsAsync(tenantId, projectId, patchRequest);
                return new OkObjectResult(result);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Patch associations failed for project {ProjectId}", projectId);
                return new BadRequestObjectResult(ex.Message);
            }
        }

        [FunctionName("PatchProjectScopes")]
        public async Task<IActionResult> PatchProjectScopesAsync(
            [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "projects/{tenantId}/{projectId}/scopes")]
            HttpRequest req,
            string tenantId,
            string projectId,
            ILogger log)
        {
            var authResult = await EnsureManagerAuthorizationAsync(req, tenantId, projectId);
            if (authResult != null)
            {
                return authResult;
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var patchRequest = ParseScopePatchRequest(requestBody);

            try
            {
                var result = await _projectDb.PatchProjectScopesAsync(tenantId, projectId, patchRequest);
                await _scopeSyncPublisher.PublishAsync(result.ScopeSync);
                return new OkObjectResult(result);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Patch scopes failed for project {ProjectId}", projectId);
                return new BadRequestObjectResult(ex.Message);
            }
        }

        [FunctionName("LinkProjectScopeGroupTaskSets")]
        public async Task<IActionResult> LinkProjectScopeGroupTaskSetsAsync(
            [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "projects/{tenantId}/{projectId}/scopes/link-gts")]
            HttpRequest req,
            string tenantId,
            string projectId,
            ILogger log)
        {
            var authFailure = EnsureScopeSyncAuthorization(req);
            if (authFailure != null)
            {
                return authFailure;
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var linkRequest = ParseScopeGtsLinkRequest(requestBody);

            try
            {
                var result = await _projectDb.LinkScopeGroupTaskSetsAsync(tenantId, projectId, linkRequest);
                return new OkObjectResult(result);
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("CONFLICT:", StringComparison.OrdinalIgnoreCase))
            {
                return new ConflictObjectResult(ex.Message.Substring("CONFLICT:".Length).Trim());
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Scope-to-GTS link callback failed for project {ProjectId}", projectId);
                return new BadRequestObjectResult(ex.Message);
            }
        }

        [FunctionName("GetActiveProjectsByTenant")]
        public async Task<IActionResult> GetActiveProjectsByTenant(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "projects/active/{tenantId}")]
            HttpRequest req,
            string tenantId,
            ILogger log)
        {
            log.LogInformation("GetActiveProjectsByTenant started. TenantId={TenantId}", tenantId);

            if (string.IsNullOrWhiteSpace(tenantId))
            {
                return new BadRequestObjectResult("TenantId is required.");
            }

            try
            {
                var projects = await _projectDb.GetActiveProjectsByTenantAsync(tenantId);
                return new OkObjectResult(projects);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Unhandled exception in GetActiveProjectsByTenant. TenantId={TenantId}", tenantId);
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        [FunctionName("GetProjectAssociations")]
        public async Task<IActionResult> GetProjectAssociations(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "projects/{tenantId}/{projectId}/associations")]
            HttpRequest req,
            string tenantId,
            string projectId,
            ILogger log)
        {
            try
            {
                var mode = req.Query["mode"].ToString()?.ToLower() ?? "separate";
                var role = req.Query["role"].ToString()?.ToLower() ?? "all";

                var result = await _projectDb.GetProjectAssociationsAsync(tenantId, projectId, mode, role);
                return new OkObjectResult(result);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error retrieving project associations");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        [FunctionName("GetProjectsBatch")]
        public async Task<IActionResult> GetProjectsBatch(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "projects/batch")]
            HttpRequest req,
            ILogger log)
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonConvert.DeserializeObject<ProjectBatchRequest>(body);

            if (request == null ||
                string.IsNullOrWhiteSpace(request.TenantId) ||
                request.ProjectIds == null ||
                !request.ProjectIds.Any())
            {
                return new BadRequestObjectResult("Invalid request payload.");
            }

            var projects = await _projectDb.GetProjectsByIdListAsync(request.ProjectIds, request.TenantId);
            var response = new ProjectBatchResponse
            {
                Projects = projects.Values.ToList()
            };

            return new OkObjectResult(response);
        }

        [FunctionName("GetProjectIdsForManager")]
        public async Task<IActionResult> GetProjectIdsForManager(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "projects/managed/{tenantId}/{manager}")]
            HttpRequest req,
            string tenantId,
            string manager)
        {
            var projectIds = await _projectDb.GetProjectIdsForManagerAsync(manager, tenantId);
            return new OkObjectResult(projectIds);
        }

        private async Task<IActionResult> EnsureManagerAuthorizationAsync(
            HttpRequest req,
            string tenantId,
            string projectId)
        {
            var managerEmail = GetCallerManagerEmail(req);
            if (string.IsNullOrWhiteSpace(managerEmail))
            {
                return new UnauthorizedObjectResult(
                    "Manager email is required. Provide x-user-email header or managerEmail query parameter.");
            }

            var authorized = await _projectDb.IsManagerForProjectAsync(tenantId, projectId, managerEmail);
            if (!authorized)
            {
                return new ObjectResult("Caller is not authorized to edit this project.")
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }

            return null;
        }

        private IActionResult EnsureScopeSyncAuthorization(HttpRequest req)
        {
            var expectedSecret = _configuration["ScopeSyncCallbackSecret"];
            if (string.IsNullOrWhiteSpace(expectedSecret))
            {
                return null;
            }

            var providedSecret = req.Headers["x-scope-sync-secret"].FirstOrDefault()
                                 ?? req.Query["scopeSyncSecret"].ToString();

            if (!string.Equals(expectedSecret, providedSecret, StringComparison.Ordinal))
            {
                return new UnauthorizedObjectResult("Invalid scope sync callback secret.");
            }

            return null;
        }

        private static string GetCallerManagerEmail(HttpRequest req)
        {
            var headerCandidate = req.Headers["x-user-email"].FirstOrDefault()
                                  ?? req.Headers["x-manager-email"].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(headerCandidate))
            {
                return headerCandidate.Trim();
            }

            var queryCandidate = req.Query["managerEmail"].ToString();
            if (!string.IsNullOrWhiteSpace(queryCandidate))
            {
                return queryCandidate.Trim();
            }

            queryCandidate = req.Query["userEmail"].ToString();
            return string.IsNullOrWhiteSpace(queryCandidate) ? null : queryCandidate.Trim();
        }

        private static string ReadString(JObject payload, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (payload.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var token) &&
                    token != null &&
                    token.Type != JTokenType.Null)
                {
                    var value = token.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value.Trim();
                    }
                }
            }

            return null;
        }

        private static List<string> ReadEmailList(JObject payload, params string[] keys)
        {
            var emails = new List<string>();

            foreach (var key in keys)
            {
                if (!payload.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var token) || token == null)
                {
                    continue;
                }

                if (token.Type == JTokenType.Array)
                {
                    foreach (var entry in token.Children())
                    {
                        if (entry.Type == JTokenType.String)
                        {
                            var email = entry.ToString().Trim();
                            if (!string.IsNullOrWhiteSpace(email))
                            {
                                emails.Add(email);
                            }
                        }
                        else if (entry.Type == JTokenType.Object)
                        {
                            var email = ReadString((JObject)entry, "personEmail", "PersonEmail", "email", "Email");
                            if (!string.IsNullOrWhiteSpace(email))
                            {
                                emails.Add(email);
                            }
                        }
                    }
                }
            }

            return emails
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<float> ReadEmbeddingArray(JObject payload, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!payload.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var token) || token == null)
                {
                    continue;
                }

                if (token.Type == JTokenType.Array)
                {
                    return token.Children()
                        .Where(item => item.Type == JTokenType.Float || item.Type == JTokenType.Integer)
                        .Select(item => item.Value<float>())
                        .ToList();
                }
            }

            return new List<float>();
        }

        private static ProjectMetadataPatchRequest ParseMetadataPatchRequest(string requestBody)
        {
            if (string.IsNullOrWhiteSpace(requestBody))
            {
                return new ProjectMetadataPatchRequest();
            }

            var payload = JObject.Parse(requestBody);
            return new ProjectMetadataPatchRequest
            {
                ProjectName = ReadString(payload, "projectName", "ProjectName", "projectNames", "ProjectNames"),
                ProjectDescription = ReadString(payload, "projectDescription", "ProjectDescription"),
                ProjectType = ReadString(payload, "projectType", "ProjectType"),
                ProjectStatus = ReadString(payload, "projectStatus", "ProjectStatus"),
                ExtProjectId = ReadString(payload, "extProjectId", "ExtProjectID", "ExtProjectId")
            };
        }

        private static ProjectAssociationPatchRequest ParseAssociationPatchRequest(string requestBody)
        {
            var payload = string.IsNullOrWhiteSpace(requestBody)
                ? new JObject()
                : JObject.Parse(requestBody);

            return new ProjectAssociationPatchRequest
            {
                Members = ReadEmailList(payload, "members", "associatedPeople", "people", "AssociatedPeople"),
                Managers = ReadEmailList(payload, "managers", "associatedManagers", "AssociatedManagers")
            };
        }

        private static ProjectScopePatchRequest ParseScopePatchRequest(string requestBody)
        {
            var payload = string.IsNullOrWhiteSpace(requestBody)
                ? new JObject()
                : JObject.Parse(requestBody);

            JToken scopeToken = null;
            foreach (var key in new[] { "scopes", "projectScopes", "ProjectScopes" })
            {
                if (payload.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var found))
                {
                    scopeToken = found;
                    break;
                }
            }

            var scopes = new List<ProjectScopePatchItem>();
            if (scopeToken?.Type == JTokenType.Array)
            {
                foreach (var scopeEntry in scopeToken.Children())
                {
                    if (scopeEntry.Type == JTokenType.String)
                    {
                        scopes.Add(new ProjectScopePatchItem
                        {
                            ProjectScopeAreaTitle = null,
                            ProjectScopeArea = scopeEntry.ToString().Trim(),
                            ProjectScopeAreaEmbeddings = new List<float>()
                        });
                        continue;
                    }

                    if (scopeEntry.Type != JTokenType.Object)
                    {
                        continue;
                    }

                    var scopeObject = (JObject)scopeEntry;
                    scopes.Add(new ProjectScopePatchItem
                    {
                        ScopeId = ReadString(scopeObject, "scopeId", "ScopeID", "scopeid"),
                        ProjectScopeAreaTitle = ReadString(
                            scopeObject,
                            "projectScopeAreaTitle",
                            "ProjectScopeAreaTitle",
                            "scopeAreaTitle",
                            "title"),
                        ProjectScopeArea = ReadString(scopeObject, "projectScopeArea", "ProjectScopeArea", "scopeArea", "name"),
                        ProjectScopeAreaEmbeddings = ReadEmbeddingArray(
                            scopeObject,
                            "projectScopeAreaEmbeddings",
                            "ProjectScopeAreaEmbeddings")
                    });
                }
            }

            return new ProjectScopePatchRequest { Scopes = scopes };
        }

        private static ProjectScopeGtsLinkRequest ParseScopeGtsLinkRequest(string requestBody)
        {
            var payload = string.IsNullOrWhiteSpace(requestBody)
                ? new JObject()
                : JObject.Parse(requestBody);

            JToken mappingToken = null;
            foreach (var key in new[] { "mappings", "Mappings", "links", "Links" })
            {
                if (payload.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var found))
                {
                    mappingToken = found;
                    break;
                }
            }

            var mappings = new List<ProjectScopeGtsLinkItem>();
            if (mappingToken?.Type == JTokenType.Array)
            {
                foreach (var mappingEntry in mappingToken.Children())
                {
                    if (mappingEntry.Type != JTokenType.Object)
                    {
                        continue;
                    }

                    var mappingObject = (JObject)mappingEntry;
                    mappings.Add(new ProjectScopeGtsLinkItem
                    {
                        ScopeId = ReadString(mappingObject, "scopeId", "ScopeID", "scopeid"),
                        GroupTaskSetId = ReadString(
                            mappingObject,
                            "groupTaskSetId",
                            "GroupTaskSetID",
                            "gtsId",
                            "id"),
                        OrchestrationRunId = ReadString(
                            mappingObject,
                            "orchestrationRunId",
                            "OrchestrationRunId",
                            "runId",
                            "RunId")
                    });
                }
            }

            return new ProjectScopeGtsLinkRequest { Mappings = mappings };
        }

        private static ProjectCreateRequest ParseCreateRequest(string requestBody)
        {
            var payload = string.IsNullOrWhiteSpace(requestBody)
                ? new JObject()
                : JObject.Parse(requestBody);

            var associations = ParseAssociationPatchRequest(requestBody);
            var scopes = ParseScopePatchRequest(requestBody);
            var metadata = ParseMetadataPatchRequest(requestBody);

            return new ProjectCreateRequest
            {
                ProjectName = metadata.ProjectName,
                ProjectDescription = metadata.ProjectDescription,
                ProjectType = metadata.ProjectType,
                ProjectStatus = metadata.ProjectStatus,
                ExtProjectId = metadata.ExtProjectId,
                Members = associations.Members,
                Managers = associations.Managers,
                Scopes = scopes.Scopes,
                TenantId = ReadString(payload, "tenantId", "TenantID", "tenantid", "tenant")
            };
        }

        private static string NormalizeEmail(string email)
        {
            return (email ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
