using System.Net;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Taslow.Project.DAL.Interface;
using Taslow.Project.Model;
using Taslow.Project.Service.Interface;
using Taslow.Shared.Model;

namespace Taslow.Project.Function;

public sealed class ProjectManagementFunctions
{
    private readonly IProjectDBUtil _projectDb;
    private readonly IProjectScopeSyncPublisher _scopeSyncPublisher;
    private readonly ILogger<ProjectManagementFunctions> _logger;

    public ProjectManagementFunctions(
        IProjectDBUtil projectDb,
        IProjectScopeSyncPublisher scopeSyncPublisher,
        ILogger<ProjectManagementFunctions> logger)
    {
        _projectDb = projectDb;
        _scopeSyncPublisher = scopeSyncPublisher;
        _logger = logger;
    }

    [Function("CreateProjectV2")]
    public async Task<HttpResponseData> CreateProjectV2Async(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "projects/{tenantId}")] HttpRequestData req,
        string tenantId)
    {
        var request = await ReadBodyAsync<ProjectCreateRequest>(req);
        if (request == null || string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(request.ProjectName))
        {
            return await Json(req, HttpStatusCode.BadRequest, "tenantId and projectName are required.");
        }

        var now = DateTime.UtcNow;
        var project = new TaskProject
        {
            Id = Guid.NewGuid().ToString(),
            tenantid = tenantId.Trim(),
            ProjectNames = request.ProjectName.Trim(),
            projectdescription = request.ProjectDescription.Trim(),
            projecttype = request.ProjectType.Trim(),
            projectstatus = string.IsNullOrWhiteSpace(request.ProjectStatus) ? "Active" : request.ProjectStatus.Trim(),
            ExtProjectID = request.ExtProjectId.Trim(),
            datecreated = now,
            lastmodifieddate = now
        };

        try
        {
            if (!await _projectDb.InsertProject(project))
            {
                return await Json(req, HttpStatusCode.BadRequest, "Could not create project.");
            }

            var managers = request.Managers.ToList();
            var callerManager = GetCallerManagerEmail(req);
            if (!string.IsNullOrWhiteSpace(callerManager)
                && !managers.Contains(callerManager, StringComparer.OrdinalIgnoreCase))
            {
                managers.Add(callerManager);
            }

            if (request.Members.Any() || managers.Any())
            {
                await _projectDb.PatchProjectAssociationsAsync(
                    tenantId,
                    project.Id,
                    new ProjectAssociationPatchRequest { Members = request.Members, Managers = managers });
            }

            if (request.Scopes.Any())
            {
                var scopeResult = await _projectDb.PatchProjectScopesAsync(
                    tenantId,
                    project.Id,
                    new ProjectScopePatchRequest { Scopes = request.Scopes });
                await _scopeSyncPublisher.PublishAsync(scopeResult.ScopeSync);
            }

            var detail = await _projectDb.GetProjectDetailAsync(tenantId, project.Id);
            return await Json(req, HttpStatusCode.OK, detail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Project creation failed. TenantId={TenantId}", tenantId);
            return await Json(req, HttpStatusCode.BadRequest, ex.Message);
        }
    }

    [Function("GetProjectDetail")]
    public async Task<HttpResponseData> GetProjectDetailAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "projects/{tenantId}/{projectId}/detail")] HttpRequestData req,
        string tenantId,
        string projectId)
    {
        var authFailure = await EnsureManagerAuthorizationAsync(req, tenantId, projectId);
        if (authFailure != null)
        {
            return authFailure;
        }

        var detail = await _projectDb.GetProjectDetailAsync(tenantId, projectId);
        return detail == null
            ? await Json(req, HttpStatusCode.NotFound, "Project not found.")
            : await Json(req, HttpStatusCode.OK, detail);
    }

    [Function("PatchProjectMetadata")]
    public Task<HttpResponseData> PatchProjectMetadataAsync(
        [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "projects/{tenantId}/{projectId}/metadata")] HttpRequestData req,
        string tenantId,
        string projectId)
        => ExecuteManagerPatchAsync(
            req,
            tenantId,
            projectId,
            async () => await _projectDb.PatchProjectMetadataAsync(
                tenantId,
                projectId,
                await ReadBodyAsync<ProjectMetadataPatchRequest>(req) ?? new ProjectMetadataPatchRequest()),
            "metadata");

    [Function("PatchProjectAssociations")]
    public Task<HttpResponseData> PatchProjectAssociationsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "projects/{tenantId}/{projectId}/associations")] HttpRequestData req,
        string tenantId,
        string projectId)
        => ExecuteManagerPatchAsync(
            req,
            tenantId,
            projectId,
            async () => await _projectDb.PatchProjectAssociationsAsync(
                tenantId,
                projectId,
                await ReadBodyAsync<ProjectAssociationPatchRequest>(req) ?? new ProjectAssociationPatchRequest()),
            "associations");

    [Function("PatchProjectScopes")]
    public async Task<HttpResponseData> PatchProjectScopesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "projects/{tenantId}/{projectId}/scopes")] HttpRequestData req,
        string tenantId,
        string projectId)
    {
        var authFailure = await EnsureManagerAuthorizationAsync(req, tenantId, projectId);
        if (authFailure != null)
        {
            return authFailure;
        }

        try
        {
            var request = await ReadBodyAsync<ProjectScopePatchRequest>(req) ?? new ProjectScopePatchRequest();
            var result = await _projectDb.PatchProjectScopesAsync(tenantId, projectId, request);
            await _scopeSyncPublisher.PublishAsync(result.ScopeSync);
            return await Json(req, HttpStatusCode.OK, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Project scope patch failed. TenantId={TenantId}, ProjectId={ProjectId}", tenantId, projectId);
            return await Json(req, HttpStatusCode.BadRequest, ex.Message);
        }
    }

    private async Task<HttpResponseData> ExecuteManagerPatchAsync<T>(
        HttpRequestData req,
        string tenantId,
        string projectId,
        Func<Task<T>> operation,
        string patchType)
    {
        var authFailure = await EnsureManagerAuthorizationAsync(req, tenantId, projectId);
        if (authFailure != null)
        {
            return authFailure;
        }

        try
        {
            return await Json(req, HttpStatusCode.OK, await operation());
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Project {PatchType} patch failed. TenantId={TenantId}, ProjectId={ProjectId}",
                patchType,
                tenantId,
                projectId);
            return await Json(req, HttpStatusCode.BadRequest, ex.Message);
        }
    }

    private async Task<HttpResponseData?> EnsureManagerAuthorizationAsync(
        HttpRequestData req,
        string tenantId,
        string projectId)
    {
        var managerEmail = GetCallerManagerEmail(req);
        if (string.IsNullOrWhiteSpace(managerEmail))
        {
            return await Json(
                req,
                HttpStatusCode.Unauthorized,
                "Manager email is required. Provide x-user-email header or managerEmail query parameter.");
        }

        if (!await _projectDb.IsManagerForProjectAsync(tenantId, projectId, managerEmail))
        {
            return await Json(req, HttpStatusCode.Forbidden, "Caller is not authorized to edit this project.");
        }

        return null;
    }

    private static string? GetCallerManagerEmail(HttpRequestData req)
    {
        var header = GetHeader(req, "x-user-email") ?? GetHeader(req, "x-manager-email");
        if (!string.IsNullOrWhiteSpace(header))
        {
            return header.Trim();
        }

        return GetQueryValue(req, "managerEmail") ?? GetQueryValue(req, "userEmail");
    }

    private static async Task<T?> ReadBodyAsync<T>(HttpRequestData req) where T : class
    {
        using var reader = new StreamReader(req.Body);
        var body = await reader.ReadToEndAsync();
        return string.IsNullOrWhiteSpace(body) ? null : JsonConvert.DeserializeObject<T>(body);
    }

    private static async Task<HttpResponseData> Json<T>(HttpRequestData req, HttpStatusCode status, T payload)
    {
        var response = req.CreateResponse(status);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonConvert.SerializeObject(payload), Encoding.UTF8);
        return response;
    }

    private static string? GetHeader(HttpRequestData req, string name)
        => req.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    private static string? GetQueryValue(HttpRequestData req, string name)
    {
        foreach (var part in req.Url.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length > 0
                && string.Equals(Uri.UnescapeDataString(pair[0]), name, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Length == 2 ? Uri.UnescapeDataString(pair[1].Replace('+', ' ')).Trim() : string.Empty;
            }
        }

        return null;
    }
}
