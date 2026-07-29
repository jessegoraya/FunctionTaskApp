using System.Net;
using System.Text;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Taslow.Project.Model;
using Taslow.Project.Service;
using Taslow.Project.Service.Interface;
using Taslow.Shared.Model;
using Taslow.Shared.Security;

namespace Taslow.Project.Function;

public sealed class ProjectTaskController
{
    private readonly IProjectService _projectService;
    private readonly IProjectRequestValidator _validator;
    private readonly IProjectAuthorizationService _authorizationService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProjectTaskController> _logger;

    public ProjectTaskController(
        IProjectService projectService,
        IProjectRequestValidator validator,
        IProjectAuthorizationService authorizationService,
        IConfiguration configuration,
        ILogger<ProjectTaskController> logger)
    {
        _projectService = projectService;
        _validator = validator;
        _authorizationService = authorizationService;
        _configuration = configuration;
        _logger = logger;
    }

    [Function("CreateProject")]
    public async Task<HttpResponseData> RunCreateProjectAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");

        var data = await ReadBodyAsync<TaskProject>(req);
        var tenant = GetQueryValue(req, "tenant") ?? data?.tenantid;
        if (tenant == null)
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        var authFailure = Authorize(req, tenant, out var auth);
        if (authFailure != null)
        {
            return authFailure;
        }
        try
        {
            _authorizationService.EnsureCanCreate(auth, tenant);
        }
        catch (ProjectAuthorizationException ex)
        {
            return AuthorizationFailure(req, ex);
        }

        try
        {
            var response = await _projectService.CreateAsync(data!);
            return await Json(req, HttpStatusCode.OK, response);
        }
        catch (Exception)
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }
    }

    [Function("GetActiveProjectsByTenant")]
    public async Task<HttpResponseData> GetActiveProjectsByTenant(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "projects/active/{tenantId}")] HttpRequestData req,
        string tenantId)
    {
        _logger.LogInformation("GetActiveProjectsByTenant started. TenantId={TenantId}", tenantId);

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return await Json(req, HttpStatusCode.BadRequest, "TenantId is required.");
        }

        var authFailure = Authorize(req, tenantId, out var auth);
        if (authFailure != null)
        {
            return authFailure;
        }

        if (!await _projectService.IsTenantActiveAsync(tenantId))
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        try
        {
            var projects = await _projectService.GetActiveProjectsByTenantAsync(tenantId);
            var visibleProjects = ProjectAccessPolicy.FilterVisible(auth, projects).ToList();
            return await Json(req, HttpStatusCode.OK, visibleProjects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in GetActiveProjectsByTenant. TenantId={TenantId}", tenantId);
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }

    [Function("GetProjectAssociations")]
    public async Task<HttpResponseData> GetProjectAssociations(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "projects/{tenantId}/{projectId}/associations")] HttpRequestData req,
        string tenantId,
        string projectId)
    {
        var authFailure = Authorize(req, tenantId, out _);
        if (authFailure != null)
        {
            return authFailure;
        }

        try
        {
            var mode = (GetQueryValue(req, "mode") ?? "separate").ToLowerInvariant();
            var role = (GetQueryValue(req, "role") ?? "all").ToLowerInvariant();
            var result = await _projectService.GetProjectAssociationsAsync(tenantId, projectId, mode, role);
            return await Json(req, HttpStatusCode.OK, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving project associations");
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }

    [Function("GetProjectsBatch")]
    public async Task<HttpResponseData> GetProjectsBatch(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "projects/batch")] HttpRequestData req)
    {
        var request = await ReadBodyAsync<ProjectBatchRequest>(req);
        if (!_validator.IsValid(request))
        {
            return await Json(req, HttpStatusCode.BadRequest, "Invalid request payload.");
        }

        var authFailure = Authorize(req, request!.TenantId, out var auth);
        if (authFailure != null)
        {
            return authFailure;
        }

        var projects = await _projectService.GetProjectsByIdListAsync(request!.ProjectIds, request.TenantId);
        var visibleProjects = ProjectAccessPolicy.FilterVisible(auth, projects.Values).ToList();
        return await Json(req, HttpStatusCode.OK, new ProjectBatchResponse { Projects = visibleProjects });
    }

    [Function("GetProjectAgentContextBatch")]
    public async Task<HttpResponseData> GetProjectAgentContextBatch(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "internal/projects/agent-context/batch")] HttpRequestData req)
    {
        if (!WorkloadRequestAuthorizer.IsEmailIngestionAuthorized(
            GetHeader(req, WorkloadRequestAuthorizer.HeaderName)))
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        var request = await ReadBodyAsync<ProjectAgentContextRequest>(req);
        if (!_validator.IsValid(request))
        {
            return await Json(req, HttpStatusCode.BadRequest, "Invalid request payload.");
        }

        try
        {
            var response = await _projectService.GetProjectAgentContextBatchAsync(request!);
            return await Json(req, HttpStatusCode.OK, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in GetProjectAgentContextBatch. TenantId={TenantId}", request!.TenantId);
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }

    [Function("PatchProjectClientDomains")]
    public async Task<HttpResponseData> PatchProjectClientDomains(
        [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "internal/projects/client-domains")] HttpRequestData req)
    {
        var request = await ReadBodyAsync<ProjectClientDomainsPatchRequest>(req);
        if (!_validator.IsValid(request))
        {
            return await Json(req, HttpStatusCode.BadRequest, "Invalid request payload.");
        }

        try
        {
            var updated = await _projectService.UpdateProjectClientDomainsAsync(request!);
            return await Json(req, HttpStatusCode.OK, new { updated });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled exception in PatchProjectClientDomains. TenantId={TenantId}, ProjectId={ProjectId}",
                request!.TenantId,
                request.ProjectId);
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }

    [Function("LinkProjectScopeGroupTaskSets")]
    public async Task<HttpResponseData> LinkProjectScopeGroupTaskSets(
        [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "projects/{tenantId}/{projectId}/scopes/link-gts")] HttpRequestData req,
        string tenantId,
        string projectId)
    {
        var expectedSecret = _configuration["ScopeSyncCallbackSecret"];
        var providedSecret = GetHeader(req, "x-scope-sync-secret");
        if (!_validator.IsCallbackAuthorized(expectedSecret, providedSecret))
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        var request = await ReadBodyAsync<ProjectScopeLinkRequest>(req);
        if (!_validator.IsValid(request, tenantId, projectId))
        {
            return await Json(req, HttpStatusCode.BadRequest, "Invalid project scope link payload.");
        }

        try
        {
            var response = await _projectService.LinkProjectScopeGroupTaskSetsAsync(request!);
            return await Json(req, response.Updated ? HttpStatusCode.OK : HttpStatusCode.NotFound, response);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning(
                ex,
                "Project scope link target not found. TenantId={TenantId}, ProjectId={ProjectId}",
                tenantId,
                projectId);
            return await Json(req, HttpStatusCode.NotFound, new { tenantId, projectId });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled exception in LinkProjectScopeGroupTaskSets. TenantId={TenantId}, ProjectId={ProjectId}",
                tenantId,
                projectId);
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }

    [Function("GetProjectIdsForManager")]
    public async Task<HttpResponseData> GetProjectIdsForManager(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "projects/managed/{tenantId}/{manager}")] HttpRequestData req,
        string tenantId,
        string manager)
    {
        var authFailure = Authorize(req, tenantId, out var auth);
        if (authFailure != null)
        {
            return authFailure;
        }
        try
        {
            _authorizationService.EnsureCanReadManagedProjects(auth, tenantId, manager);
        }
        catch (ProjectAuthorizationException ex)
        {
            return AuthorizationFailure(req, ex);
        }

        var projectIds = await _projectService.GetProjectIdsForManagerAsync(manager, tenantId);
        return await Json(req, HttpStatusCode.OK, projectIds);
    }

    private HttpResponseData? Authorize(
        HttpRequestData req,
        string tenantId,
        out ProjectAuthContext auth)
    {
        try
        {
            auth = _authorizationService.Resolve(ToDictionary(req));
            _authorizationService.EnsureTenant(auth, tenantId);
            return null;
        }
        catch (ProjectAuthorizationException ex)
        {
            auth = new ProjectAuthContext();
            return AuthorizationFailure(req, ex);
        }
    }

    private static HttpResponseData AuthorizationFailure(
        HttpRequestData req,
        ProjectAuthorizationException exception)
    {
        var response = req.CreateResponse(exception.StatusCode);
        response.Headers.Add("x-taslow-error-code", exception.Code);
        return response;
    }

    private static Dictionary<string, string> ToDictionary(HttpRequestData req)
        => req.Headers.ToDictionary(
            header => header.Key,
            header => string.Join(",", header.Value),
            StringComparer.OrdinalIgnoreCase);

    private static async Task<T?> ReadBodyAsync<T>(HttpRequestData req) where T : class
    {
        using var reader = new StreamReader(req.Body);
        var body = await reader.ReadToEndAsync();
        return string.IsNullOrWhiteSpace(body) ? null : JsonConvert.DeserializeObject<T>(body);
    }

    private static async Task<HttpResponseData> Json<T>(HttpRequestData req, HttpStatusCode statusCode, T payload)
    {
        var response = req.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonConvert.SerializeObject(payload), Encoding.UTF8);
        return response;
    }

    private static string? GetHeader(HttpRequestData req, string name)
        => req.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    private static string? GetQueryValue(HttpRequestData req, string name)
    {
        var query = req.Url.Query.TrimStart('?');
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length > 0 && string.Equals(Uri.UnescapeDataString(pair[0]), name, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Length == 2 ? Uri.UnescapeDataString(pair[1].Replace('+', ' ')) : string.Empty;
            }
        }

        return null;
    }
}
