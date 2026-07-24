using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Taslow.Shared.Model;
using Taslow.Shared.Security;
using Taslow.Task.Client.Interface;
using Taslow.Task.DAL.Interface;
using Taslow.Task.Model;
using Taslow.Task.Service;
using Taslow.Task.Service.Interface;

namespace Taslow.Task.Function
{
    public class FunctionTaskController
    {
        private readonly ITaskDBUtil _taskDb;
        private readonly IProjectServiceClient _projSvcClient;
        private readonly IAnalyticsService _analyticsService;
        private readonly ILogger<FunctionTaskController> _log;

        public FunctionTaskController(
            ITaskDBUtil taskDb,
            IProjectServiceClient projSvcClient,
            IAnalyticsService analyticsService,
            ILogger<FunctionTaskController> log)
        {
            _taskDb = taskDb;
            _projSvcClient = projSvcClient;
            _analyticsService = analyticsService;
            _log = log;
        }

        [Function("Ping")]
        public Task<HttpResponseData> Ping(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ping")] HttpRequestData req)
        {
            return TextAsync(req, HttpStatusCode.OK, "pong");
        }

        [Function("AddGroupTaskSet")]
        public async Task<HttpResponseData> RunAddGroupTaskSetAsync(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "grouptaskset")] HttpRequestData req)
        {
            _log.LogInformation("AddGroupTaskSet function processed a request.");

            string requestBody = await ReadBodyAsync(req);
            GroupTaskSet newGTS = JsonConvert.DeserializeObject<GroupTaskSet>(requestBody);

            if (newGTS == null)
            {
                return await TextAsync(req, HttpStatusCode.BadRequest, "Invalid payload");
            }

            newGTS.id = Guid.NewGuid().ToString();

            GroupTaskSet result = await _taskDb.InsertGroupTaskSet(newGTS);

            if (result != null && !string.IsNullOrEmpty(result.id))
            {
                return await JsonAsync(req, HttpStatusCode.OK, result);
            }

            return await TextAsync(req, HttpStatusCode.BadRequest, "Could not add GroupTaskSet");
        }

        [Function("GetGroupTaskSetById")]
        public async Task<HttpResponseData> RunGetGroupTaskSetByIdAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "grouptaskset/{id}/{tenantid}")] HttpRequestData req,
            string id,
            string tenantid)
        {
            _log.LogInformation(
                "GetGroupTaskSetById function processed a request for id: {Id}, tenantid: {TenantId}",
                id,
                tenantid);

            GroupTaskSet result = await _taskDb.GetGroupTaskSet(id, tenantid);

            return result != null
                ? await JsonAsync(req, HttpStatusCode.OK, result)
                : req.CreateResponse(HttpStatusCode.NotFound);
        }

        [Function("GetInternalGroupTaskExistence")]
        public async Task<HttpResponseData> RunGetInternalGroupTaskExistenceAsync(
            [HttpTrigger(
                AuthorizationLevel.Function,
                "get",
                Route = "internal/tasks/group-task-exists/{tenantid}/{groupTaskSetId}/{groupTaskId}")] HttpRequestData req,
            string tenantid,
            string groupTaskSetId,
            string groupTaskId)
        {
            if (!WorkloadRequestAuthorizer.IsEmailIngestionAuthorized(
                FirstHeader(req, WorkloadRequestAuthorizer.HeaderName)))
            {
                return req.CreateResponse(HttpStatusCode.Unauthorized);
            }

            if (string.IsNullOrWhiteSpace(tenantid)
                || string.IsNullOrWhiteSpace(groupTaskSetId)
                || !Guid.TryParse(groupTaskId, out var requestedGroupTaskId))
            {
                return await TextAsync(req, HttpStatusCode.BadRequest, "Valid tenant, Group Task Set, and Group Task identifiers are required.");
            }

            var taskSet = await _taskDb.GetGroupTaskSet(groupTaskSetId, tenantid);
            if (taskSet == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var exists = (taskSet.grouptask ?? new List<GroupTask>()).Any(task =>
                Guid.TryParse(task.grouptaskid, out var existingGroupTaskId)
                && existingGroupTaskId == requestedGroupTaskId);
            return await JsonAsync(req, HttpStatusCode.OK, new { exists });
        }

        [Function("GetInternalEmailE2EGroupTaskEvidence")]
        public async Task<HttpResponseData> RunGetInternalEmailE2EGroupTaskEvidenceAsync(
            [HttpTrigger(
                AuthorizationLevel.Function,
                "get",
                Route = "internal/email-e2e/tasks/{tenantid}/{groupTaskSetId}/{groupTaskId}/{idempotencyKey}")] HttpRequestData req,
            string tenantid,
            string groupTaskSetId,
            string groupTaskId,
            string idempotencyKey)
        {
            if (!WorkloadRequestAuthorizer.IsEmailE2ETestRunnerAuthorized(
                FirstHeader(req, WorkloadRequestAuthorizer.HeaderName)))
            {
                return req.CreateResponse(HttpStatusCode.Unauthorized);
            }

            if (!Guid.TryParse(groupTaskId, out var requestedGroupTaskId)
                || !IsSha256(idempotencyKey))
            {
                return await TextAsync(
                    req,
                    HttpStatusCode.BadRequest,
                    "Valid Group Task and idempotency identifiers are required.");
            }

            var taskSet = await _taskDb.GetGroupTaskSet(groupTaskSetId, tenantid);
            if (taskSet == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var task = (taskSet.grouptask ?? new List<GroupTask>()).SingleOrDefault(candidate =>
                Guid.TryParse(candidate.grouptaskid, out var existingGroupTaskId)
                && existingGroupTaskId == requestedGroupTaskId);
            if (task == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var sourceMatches = HasCampaignSource(task, idempotencyKey);
            var assignedPeople = (task.individualtasksets ?? new List<IndividualTaskSet>())
                .SelectMany(set => set.individualtask ?? new List<IndividualTask>())
                .Select(item => item.assignedperson)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var dueDates = (task.grouptaskduedate ?? new List<Taslow.Task.Model.GroupTaskDueDate>())
                .Select(item => item.grouptaskduedate)
                .ToList();

            return await JsonAsync(req, HttpStatusCode.OK, new
            {
                exists = true,
                sourceMatches,
                tenantId = tenantid,
                groupTaskSetId,
                groupTaskId,
                projectId = taskSet.caseid,
                title = task.grouptasktitle,
                status = task.grouptaskstatus,
                type = task.grouptasktypeid,
                dueDates,
                assignedPeople,
                task.createdby,
                task.createddate,
                protectedSourceFieldsIncluded = false
            });
        }

        [Function("DeleteInternalEmailE2EGroupTask")]
        public async Task<HttpResponseData> RunDeleteInternalEmailE2EGroupTaskAsync(
            [HttpTrigger(
                AuthorizationLevel.Function,
                "delete",
                Route = "internal/email-e2e/tasks/{tenantid}/{groupTaskSetId}/{groupTaskId}/{idempotencyKey}")] HttpRequestData req,
            string tenantid,
            string groupTaskSetId,
            string groupTaskId,
            string idempotencyKey)
        {
            if (!WorkloadRequestAuthorizer.IsEmailE2ETestRunnerAuthorized(
                FirstHeader(req, WorkloadRequestAuthorizer.HeaderName)))
            {
                return req.CreateResponse(HttpStatusCode.Unauthorized);
            }

            if (!Guid.TryParse(groupTaskId, out var requestedGroupTaskId)
                || !IsSha256(idempotencyKey))
            {
                return await TextAsync(
                    req,
                    HttpStatusCode.BadRequest,
                    "Valid Group Task and idempotency identifiers are required.");
            }

            var taskSet = await _taskDb.GetGroupTaskSet(groupTaskSetId, tenantid);
            var task = (taskSet?.grouptask ?? new List<GroupTask>()).SingleOrDefault(candidate =>
                Guid.TryParse(candidate.grouptaskid, out var existingGroupTaskId)
                && existingGroupTaskId == requestedGroupTaskId);
            if (task == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            if (!HasCampaignSource(task, idempotencyKey)
                || !string.Equals(
                    task.createdby,
                    "TaslowEmailExtractionAgent",
                    StringComparison.Ordinal))
            {
                return req.CreateResponse(HttpStatusCode.Conflict);
            }

            var deleted = await _taskDb.DeleteGroupTaskAsync(
                groupTaskSetId,
                tenantid,
                groupTaskId);
            return deleted
                ? req.CreateResponse(HttpStatusCode.NoContent)
                : req.CreateResponse(HttpStatusCode.Conflict);
        }

        [Function("GetGroupTaskSetByProjectId")]
        public async Task<HttpResponseData> RunGetGroupTaskSetByProjectIdAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "grouptasksetbyproject/{projectid}/{tenantid}")] HttpRequestData req,
            string projectid,
            string tenantid)
        {
            _log.LogInformation(
                "GetGroupTaskSetByCaseId function processed a request for id: {ProjectId}, tenantid: {TenantId}",
                projectid,
                tenantid);

            GroupTaskSet result = await _taskDb.GetGroupTaskSetByProjectId(projectid, tenantid);

            return result != null
                ? await JsonAsync(req, HttpStatusCode.OK, result)
                : req.CreateResponse(HttpStatusCode.NotFound);
        }

        [Function("GetGroupTaskSetsByProjectId")]
        public async Task<HttpResponseData> RunGetGroupTaskSetsByProjectIdAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "grouptasksetsbyproject/{projectid}/{tenantid}")] HttpRequestData req,
            string projectid,
            string tenantid)
        {
            _log.LogInformation(
                "GetGroupTaskSetsByProjectId function processed a request for projectid: {ProjectId}, tenantid: {TenantId}",
                projectid,
                tenantid);

            var result = await _taskDb.GetGroupTaskSetsByProjectId(projectid, tenantid);
            return await JsonAsync(req, HttpStatusCode.OK, result ?? new List<GroupTaskSet>());
        }

        [Function("UpdateGroupTaskSet")]
        public async Task<HttpResponseData> RunUpdateGroupTaskSetAsync(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "grouptaskset/{id}/{tenantid}")] HttpRequestData req,
            string id,
            string tenantid)
        {
            _log.LogInformation(
                "UpdateGroupTaskSet function processed a request for id: {Id}, tenantid: {TenantId}",
                id,
                tenantid);

            string requestBody = await ReadBodyAsync(req);
            GroupTaskSet updatedGTS = JsonConvert.DeserializeObject<GroupTaskSet>(requestBody);

            if (updatedGTS == null)
            {
                return await TextAsync(req, HttpStatusCode.BadRequest, "Invalid payload");
            }

            bool result = await _taskDb.UpdateGroupTaskSet(id, tenantid, updatedGTS);

            return result != true
                ? await JsonAsync(req, HttpStatusCode.OK, result)
                : req.CreateResponse(HttpStatusCode.NotFound);
        }

        [Function("DeleteGroupTaskSet")]
        public async Task<HttpResponseData> RunDeleteGroupTaskSetAsync(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "grouptaskset/{id}/{tenantid}")] HttpRequestData req,
            string id,
            string tenantid)
        {
            _log.LogInformation(
                "DeleteGroupTaskSet function processed a request for id: {Id}, tenantid: {TenantId}",
                id,
                tenantid);

            bool deleted = await _taskDb.DeleteGroupTaskSet(id, tenantid);

            return deleted
                ? req.CreateResponse(HttpStatusCode.OK)
                : req.CreateResponse(HttpStatusCode.NotFound);
        }

        [Function("AddGroupTaskToGTS")]
        public async Task<HttpResponseData> AddGroupTaskToGTS(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "addgrouptasktogts/{id}/{tenantid}/")] HttpRequestData req,
            string id,
            string tenantid)
        {
            _log.LogInformation("Processing request to add a new GroupTask.");

            string requestBody = await ReadBodyAsync(req);

            try
            {
                GroupTask newGT = JsonConvert.DeserializeObject<GroupTask>(requestBody);
                if (newGT == null)
                {
                    return await TextAsync(req, HttpStatusCode.BadRequest, "Invalid GroupTask payload.");
                }

                if (string.IsNullOrEmpty(tenantid))
                {
                    return await TextAsync(req, HttpStatusCode.BadRequest, "Missing required query parameter: tenantid");
                }

                SvcUtil svc = new SvcUtil();
                newGT = svc.SetNewIDs(newGT);

                bool success = await _taskDb.CreateGroupTaskAsync(id, tenantid, newGT);
                return success
                    ? await TextAsync(req, HttpStatusCode.OK, $"GroupTask added to GroupTaskSet {id}.")
                    : req.CreateResponse(HttpStatusCode.InternalServerError);
            }
            catch (JsonException ex)
            {
                _log.LogError(ex, "Failed to deserialize GroupTask.");
                return await TextAsync(req, HttpStatusCode.BadRequest, "Malformed JSON.");
            }
        }

        [Function("UpdateGroupTaskinGTS")]
        public async Task<HttpResponseData> UpdateGroupTaskinGTS(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "updgrouptask/{id}/{tenantid}/")] HttpRequestData req,
            string id,
            string tenantid)
        {
            try
            {
                string requestBody = await ReadBodyAsync(req);
                GroupTask updGT = JsonConvert.DeserializeObject<GroupTask>(requestBody);

                if (updGT == null)
                {
                    return await TextAsync(req, HttpStatusCode.BadRequest, "Error converting json");
                }

                bool success = await _taskDb.UpdateGroupTaskAsync(id, tenantid, updGT);
                return success
                    ? await TextAsync(req, HttpStatusCode.OK, $"GroupTask added to GroupTaskSet {id}.")
                    : req.CreateResponse(HttpStatusCode.InternalServerError);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to deserialize GroupTask.");
                return await TextAsync(req, HttpStatusCode.BadRequest, "Malformed JSON.");
            }
        }

        [Function("AddIndividualTaskToGT")]
        public async Task<HttpResponseData> AddIndividualTaskToGT(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "addindtask/{id}/{tenantid}/{gtid}/")] HttpRequestData req,
            string id,
            string tenantid,
            string gtid)
        {
            _log.LogInformation("Processing request to add a new IndividualTask.");

            string requestBody = await ReadBodyAsync(req);

            try
            {
                IndividualTask newIT = JsonConvert.DeserializeObject<IndividualTask>(requestBody);
                if (newIT == null)
                {
                    return await TextAsync(req, HttpStatusCode.BadRequest, "Invalid IndividualTask payload.");
                }

                if (string.IsNullOrEmpty(tenantid))
                {
                    return await TextAsync(req, HttpStatusCode.BadRequest, "Missing required query parameter: tenantid");
                }

                SvcUtil svc = new SvcUtil();
                newIT = svc.SetNewITIDs(newIT);

                bool success = await _taskDb.CreateIndividualTaskAsync(id, tenantid, gtid, newIT);

                return success
                    ? await TextAsync(req, HttpStatusCode.OK, $"IndividualTask added to GroupTask {gtid}.")
                    : req.CreateResponse(HttpStatusCode.InternalServerError);
            }
            catch (JsonException ex)
            {
                _log.LogError(ex, "Failed to deserialize GroupTask.");
                return await TextAsync(req, HttpStatusCode.BadRequest, "Malformed JSON.");
            }
        }

        [Function("UpdateIndividualTaskinGT")]
        public async Task<HttpResponseData> UpdateIndividualTaskinGT(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "updindtask/{id}/{tenantid}/{gtid}/")] HttpRequestData req,
            string id,
            string gtid,
            string tenantid)
        {
            try
            {
                string requestBody = await ReadBodyAsync(req);
                UpdateIndividualTaskDTO updIT = JsonConvert.DeserializeObject<UpdateIndividualTaskDTO>(requestBody);

                if (updIT == null)
                {
                    return await TextAsync(req, HttpStatusCode.BadRequest, "Error converting json");
                }

                bool success = await _taskDb.UpdateIndividualTaskAsync(id, tenantid, gtid, updIT);
                return success
                    ? await TextAsync(req, HttpStatusCode.OK, $"IndividualTask added to GroupTask {gtid} with document {id}.")
                    : req.CreateResponse(HttpStatusCode.InternalServerError);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to deserialize GroupTask.");
                return await TextAsync(req, HttpStatusCode.BadRequest, "Malformed JSON.");
            }
        }

        [Function("MoveIndividualTask")]
        public async Task<HttpResponseData> MoveIndividualTask(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "moveindtask/{tenantid}/")] HttpRequestData req,
            string tenantid)
        {
            try
            {
                string requestBody = await ReadBodyAsync(req);
                JObject payload = JsonConvert.DeserializeObject<JObject>(requestBody);

                if (payload == null)
                {
                    return await TextAsync(req, HttpStatusCode.BadRequest, "Error converting json");
                }

                string ReadToken(params string[] paths)
                {
                    foreach (var path in paths)
                    {
                        var token = payload.SelectToken(path);
                        if (token != null && token.Type != JTokenType.Null)
                        {
                            var value = token.ToString();
                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                return value;
                            }
                        }
                    }

                    return null;
                }

                var moveIT = new MoveIndividualTaskDTO
                {
                    individualtaskid = ReadToken("individualtaskid", "individualTaskId", "IndividualTaskID", "itid", "taskId"),
                    sourceprojectid = ReadToken("sourceprojectid", "sourceProjectId", "source.projectid", "source.projectId", "oldProjectId", "previousProjectId", "projectId"),
                    sourcegrouptaskid = ReadToken("sourcegrouptaskid", "sourceGroupTaskId", "source.grouptaskid", "source.groupTaskId", "gtid", "groupTaskId"),
                    sourceindividualtasksetid = ReadToken("sourceindividualtasksetid", "sourceIndividualTaskSetId", "source.individualtasksetid", "source.individualTaskSetId", "itsid", "individualTaskSetId"),
                    targetprojectid = ReadToken("targetprojectid", "targetProjectId", "target.projectid", "target.projectId", "newProjectId", "projectid"),
                    targetgrouptaskid = ReadToken("targetgrouptaskid", "targetGroupTaskId", "target.grouptaskid", "target.groupTaskId", "newGroupTaskId"),
                    targetindividualtasksetid = ReadToken("targetindividualtasksetid", "targetIndividualTaskSetId", "target.individualtasksetid", "target.individualTaskSetId", "newIndividualTaskSetId"),
                    updatedby = ReadToken("updatedby", "updatedBy", "lastModifiedBy")
                };

                if (string.IsNullOrWhiteSpace(moveIT.targetgrouptaskid))
                {
                    moveIT.targetgrouptaskid = moveIT.sourcegrouptaskid;
                }

                bool success = await _taskDb.MoveIndividualTaskAsync(tenantid, moveIT);
                return success
                    ? await TextAsync(req, HttpStatusCode.OK, "IndividualTask moved successfully.")
                    : req.CreateResponse(HttpStatusCode.InternalServerError);
            }
            catch (ArgumentException ex)
            {
                _log.LogError(ex, "Invalid move request payload for tenant {TenantId}", tenantid);
                return await TextAsync(req, HttpStatusCode.BadRequest, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _log.LogError(ex, "Unable to move IndividualTask for tenant {TenantId}", tenantid);
                return await TextAsync(req, HttpStatusCode.NotFound, ex.Message);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to move IndividualTask for tenant {TenantId}", tenantid);
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }

        [Function("GetGTContextDTObyTenantandPerson")]
        public async Task<HttpResponseData> RunGetGroupTaskSetByTenantAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "taskcontextdto/{tenantid}/{person}")] HttpRequestData req,
            string tenantid,
            string person)
        {
            _log.LogInformation("GetGTSDTOyTenantandPerson function processed a request for tenantid: {TenantId}", tenantid);

            List<TaskContextDTO> result = await _taskDb.GetGTContextDTO(tenantid, person);

            return result != null
                ? await JsonAsync(req, HttpStatusCode.OK, result)
                : req.CreateResponse(HttpStatusCode.NotFound);
        }

        [Function("GetTasksForManagedProjects")]
        public async Task<HttpResponseData> GetTasksForManagedProjects(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "getmgrtaskcontextdto/{tenantid}/{manager}")] HttpRequestData req,
            string tenantid,
            string manager)
        {
            if (string.IsNullOrEmpty(manager))
            {
                return await TextAsync(req, HttpStatusCode.BadRequest, "Manager email is required.");
            }

            var projectIds = await _projSvcClient.GetProjectIdsForManagerAsync(tenantid, manager);

            if (projectIds == null || !projectIds.Any())
            {
                return await JsonAsync(req, HttpStatusCode.OK, new List<TaskContextDTO>());
            }

            var tasks = await _taskDb.GetTasksByProjectIdsAsync(tenantid, projectIds);

            return await JsonAsync(req, HttpStatusCode.OK, tasks);
        }

        [Function("GetAnalyticsPortfolio")]
        public Task<HttpResponseData> GetAnalyticsPortfolio(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "analytics/{tenantId}/portfolio")] HttpRequestData req,
            string tenantId) =>
            ExecuteAnalyticsAsync(req, () => _analyticsService.GetPortfolioAsync(
                tenantId,
                ReadUserEmail(req),
                ReadList(req, "x-taslow-roles", "x-user-roles", "x-user-role"),
                ReadList(req, "x-taslow-market-codes"),
                ReadQueryList(req, "marketCode")));

        [Function("GetAnalyticsProjectType")]
        public Task<HttpResponseData> GetAnalyticsProjectType(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "analytics/{tenantId}/project-types/{projectType}")] HttpRequestData req,
            string tenantId,
            string projectType) =>
            ExecuteAnalyticsAsync(req, () => _analyticsService.GetProjectTypeAsync(
                tenantId,
                projectType,
                ReadUserEmail(req),
                ReadList(req, "x-taslow-roles", "x-user-roles", "x-user-role"),
                ReadList(req, "x-taslow-market-codes"),
                ReadQueryList(req, "marketCode")));

        [Function("GetAnalyticsProjectHierarchy")]
        public Task<HttpResponseData> GetAnalyticsProjectHierarchy(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "analytics/{tenantId}/projects/{projectId}/hierarchy")] HttpRequestData req,
            string tenantId,
            string projectId) =>
            ExecuteAnalyticsAsync(req, () => _analyticsService.GetProjectHierarchyAsync(
                tenantId,
                projectId,
                ReadUserEmail(req),
                ReadList(req, "x-taslow-roles", "x-user-roles", "x-user-role"),
                ReadList(req, "x-taslow-market-codes")));

        private async Task<HttpResponseData> ExecuteAnalyticsAsync<T>(
            HttpRequestData req,
            Func<Task<T>> action)
        {
            try
            {
                return await JsonAsync(req, HttpStatusCode.OK, await action());
            }
            catch (UnauthorizedAccessException ex)
            {
                return await TextAsync(req, HttpStatusCode.Forbidden, ex.Message);
            }
            catch (ArgumentException ex)
            {
                return await TextAsync(req, HttpStatusCode.BadRequest, ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return await TextAsync(req, HttpStatusCode.NotFound, ex.Message);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Analytics request failed for {Path}", req.Url.AbsolutePath);
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }

        private static async Task<string> ReadBodyAsync(HttpRequestData req)
        {
            using var reader = new StreamReader(req.Body);
            return await reader.ReadToEndAsync();
        }

        private static bool HasCampaignSource(GroupTask task, string idempotencyKey)
        {
            return (task.groupetasknotes ?? string.Empty).Contains(
                $"idempotencyKey={idempotencyKey}",
                StringComparison.Ordinal);
        }

        private static bool IsSha256(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.Length == 64
                && value.All(character =>
                    character is >= '0' and <= '9'
                    || character is >= 'a' and <= 'f');
        }

        private static async Task<HttpResponseData> TextAsync(
            HttpRequestData req,
            HttpStatusCode statusCode,
            string value)
        {
            var response = req.CreateResponse(statusCode);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            await response.WriteStringAsync(value ?? string.Empty);
            return response;
        }

        private static async Task<HttpResponseData> JsonAsync(
            HttpRequestData req,
            HttpStatusCode statusCode,
            object value)
        {
            var response = req.CreateResponse(statusCode);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonConvert.SerializeObject(value));
            return response;
        }

        private static string ReadUserEmail(HttpRequestData req) =>
            FirstHeader(req, "x-user-email", "x-taslow-user-email", "x-taslow-email");

        private static string FirstHeader(HttpRequestData req, params string[] names)
        {
            foreach (var name in names)
            {
                if (req.Headers.TryGetValues(name, out var values))
                {
                    var value = values.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value.Trim();
                    }
                }
            }

            return string.Empty;
        }

        private static IReadOnlyCollection<string> ReadList(HttpRequestData req, params string[] headerNames) =>
            headerNames
                .SelectMany(name => req.Headers.TryGetValues(name, out var values) ? values : Array.Empty<string>())
                .SelectMany(value => value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        private static IReadOnlyCollection<string> ReadQueryList(HttpRequestData req, string name)
        {
            var query = ParseQuery(req.Url);
            return query.TryGetValue(name, out var values)
                ? values
                    .SelectMany(value => value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                    .Select(value => value.Trim())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : new List<string>();
        }

        private static IReadOnlyDictionary<string, List<string>> ParseQuery(Uri uri)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var query = uri.Query;
            if (string.IsNullOrWhiteSpace(query) || query == "?")
            {
                return result;
            }

            foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var pieces = part.Split('=', 2);
                var key = Uri.UnescapeDataString(pieces[0].Replace("+", " "));
                var value = pieces.Length > 1
                    ? Uri.UnescapeDataString(pieces[1].Replace("+", " "))
                    : string.Empty;

                if (!result.TryGetValue(key, out var values))
                {
                    values = new List<string>();
                    result[key] = values;
                }

                values.Add(value);
            }

            return result;
        }
    }
}
