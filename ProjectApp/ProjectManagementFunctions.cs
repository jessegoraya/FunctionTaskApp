using System.Net;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Taslow.Project.DAL.Interface;
using Taslow.Project.Model;
using Taslow.Project.Service;
using Taslow.Project.Service.Interface;
using Taslow.Shared.Model;

namespace Taslow.Project.Function;

public sealed class ProjectManagementFunctions
{
    private readonly IProjectDBUtil _projectDb;
    private readonly IProjectAuthorizationService _authorizationService;
    private readonly IProjectRequestValidator _requestValidator;
    private readonly IProjectScopeSyncPublisher _scopeSyncPublisher;
    private readonly ILogger<ProjectManagementFunctions> _logger;

    public ProjectManagementFunctions(
        IProjectDBUtil projectDb,
        IProjectAuthorizationService authorizationService,
        IProjectRequestValidator requestValidator,
        IProjectScopeSyncPublisher scopeSyncPublisher,
        ILogger<ProjectManagementFunctions> logger)
    {
        _projectDb = projectDb;
        _authorizationService = authorizationService;
        _requestValidator = requestValidator;
        _scopeSyncPublisher = scopeSyncPublisher;
        _logger = logger;
    }

    [Function("CreateProjectV2")]
    public async Task<HttpResponseData> CreateProjectV2Async(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "projects/{tenantId}")] HttpRequestData req,
        string tenantId)
    {
        var authFailure = await EnsureCreateAuthorizationAsync(req, tenantId);
        if (authFailure != null)
        {
            return authFailure;
        }

        var request = await ReadBodyAsync<ProjectCreateRequest>(req);
        if (!_requestValidator.IsValid(request, tenantId))
        {
            return await Json(
                req,
                HttpStatusCode.BadRequest,
                "Project name, canonical type, Market Code, at least one manager, valid unique people, and unique non-empty scopes are required.");
        }

        var now = DateTime.UtcNow;
        var managers = request.Managers
            .Select(email => email.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var members = request.Members
            .Select(email => email.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var project = new TaskProject
        {
            Id = Guid.NewGuid().ToString(),
            tenantid = tenantId.Trim(),
            ProjectNames = request.ProjectName.Trim(),
            projectdescription = request.ProjectDescription.Trim(),
            projecttype = request.ProjectType.Trim(),
            marketcode = request.MarketCode.Trim().ToUpperInvariant(),
            projectstatus = string.IsNullOrWhiteSpace(request.ProjectStatus) ? "Active" : request.ProjectStatus.Trim(),
            ExtProjectID = request.ExtProjectId.Trim(),
            datecreated = now,
            lastmodifieddate = now,
            associatedpeople = members.Select(email => BuildAssociatedPerson(email, "Person")).ToList(),
            associatedmanagers = managers.Select(email => BuildAssociatedPerson(email, "Manager")).ToList(),
            projectscopes = request.Scopes.Select(scope => new ProjectScope
            {
                scopeid = string.IsNullOrWhiteSpace(scope.ScopeId) ? Guid.NewGuid().ToString() : scope.ScopeId.Trim(),
                projectscopeareatitle = scope.ProjectScopeAreaTitle.Trim(),
                projectscopearea = scope.ProjectScopeArea.Trim(),
                projectscopeareaembeddings = scope.ProjectScopeAreaEmbeddings?.ToList() ?? new List<float>(),
                isarchived = false
            }).ToList()
        };

        try
        {
            if (!await _projectDb.InsertProject(project))
            {
                return await Json(req, HttpStatusCode.BadRequest, "Could not create project.");
            }

            await _scopeSyncPublisher.PublishAsync(new ProjectScopeSyncPayload
            {
                TenantId = tenantId,
                ProjectId = project.Id,
                GeneratedAtUtc = now,
                Added = project.projectscopes.Select(BuildScopeSyncItem).ToList()
            });

            var detail = await _projectDb.GetProjectDetailAsync(tenantId, project.Id);
            return await Json(req, HttpStatusCode.OK, detail);
        }
        catch (Exception ex)
        {
            var correlationId = Guid.NewGuid().ToString();
            _logger.LogError(
                ex,
                "Project creation failed. TenantId={TenantId}, CorrelationId={CorrelationId}",
                tenantId,
                correlationId);
            return await Json(req, HttpStatusCode.BadRequest, new ApiErrorResponse
            {
                Error = new ApiError
                {
                    Code = "PROJECT_CREATE_FAILED",
                    Message = "Project creation failed.",
                    CorrelationId = correlationId
                }
            });
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
        ProjectAuthContext auth;
        try
        {
            auth = _authorizationService.Resolve(ToDictionary(req));
            _authorizationService.EnsureCanManage(auth, tenantId);
        }
        catch (ProjectAuthorizationException ex)
        {
            _logger.LogWarning(
                "Project management authorization rejected. TenantId={TenantId}, Status={Status}, Code={Code}.",
                tenantId,
                (int)ex.StatusCode,
                ex.Code);
            return await AuthorizationError(req, ex);
        }

        if (!await _projectDb.IsManagerForProjectAsync(tenantId, projectId, auth.Email))
        {
            return await Json(req, HttpStatusCode.Forbidden, "Caller is not authorized to edit this project.");
        }

        return null;
    }

    private async Task<HttpResponseData?> EnsureCreateAuthorizationAsync(HttpRequestData req, string tenantId)
    {
        try
        {
            var auth = _authorizationService.Resolve(ToDictionary(req));
            _authorizationService.EnsureCanCreate(auth, tenantId);
            return null;
        }
        catch (ProjectAuthorizationException ex)
        {
            _logger.LogWarning(
                "Project creation authorization rejected. TenantId={TenantId}, Status={Status}, Code={Code}.",
                tenantId,
                (int)ex.StatusCode,
                ex.Code);
            return await AuthorizationError(req, ex);
        }
    }

    private static AssociatedPeople BuildAssociatedPerson(string email, string role)
    {
        var localPart = email.Split('@').FirstOrDefault() ?? string.Empty;
        return new AssociatedPeople
        {
            associatedpersonid = Guid.NewGuid(),
            personemail = email,
            personname = localPart.Replace('.', ' ').Replace('_', ' ').Trim(),
            role = role
        };
    }

    private static ProjectScopeSyncItem BuildScopeSyncItem(ProjectScope scope)
        => new()
        {
            ScopeId = scope.scopeid,
            ProjectScopeAreaTitle = scope.projectscopeareatitle,
            ProjectScopeArea = scope.projectscopearea,
            ProjectScopeAreaEmbeddings = scope.projectscopeareaembeddings.ToList(),
            GroupTaskSetId = scope.grouptasksetid ?? string.Empty
        };

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

    private static Task<HttpResponseData> AuthorizationError(HttpRequestData req, ProjectAuthorizationException ex)
        => Json(req, ex.StatusCode, new ApiErrorResponse
        {
            Error = new ApiError
            {
                Code = ex.Code,
                Message = ex.Message,
                CorrelationId = Guid.NewGuid().ToString()
            }
        });

    private static Dictionary<string, string> ToDictionary(HttpRequestData req)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in req.Headers)
        {
            headers[header.Key] = header.Value.FirstOrDefault() ?? string.Empty;
        }

        return headers;
    }
}
