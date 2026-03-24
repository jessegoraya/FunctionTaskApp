using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Taslow.Project.Service.Interface;
using Taslow.Shared.Model;

namespace Taslow.Project.Service
{
    public class ProjectScopeSyncPublisher : IProjectScopeSyncPublisher
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ProjectScopeSyncPublisher> _logger;

        public ProjectScopeSyncPublisher(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<ProjectScopeSyncPublisher> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> PublishAsync(ProjectScopeSyncPayload payload)
        {
            if (payload == null)
            {
                return false;
            }

            var endpoint = _configuration["ScopeSyncOrchestrationEndpoint"];
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                _logger.LogInformation(
                    "ScopeSyncOrchestrationEndpoint is not configured. Skipping scope sync publish for ProjectId={ProjectId}.",
                    payload.ProjectId);
                return false;
            }

            var addedScopes = payload.Added ?? new List<ProjectScopeSyncItem>();
            if (!addedScopes.Any())
            {
                _logger.LogInformation(
                    "No added scopes to publish for ProjectId={ProjectId}.",
                    payload.ProjectId);
                return true;
            }

            var callbackBaseUrl = _configuration["ProjectScopeLinkCallbackBaseUrl"];
            var callbackUrl = _configuration["ProjectScopeLinkCallbackUrl"];
            var callbackFunctionKey = _configuration["ProjectScopeLinkCallbackFunctionKey"];
            var resolvedCallbackUrl = callbackUrl;
            if (!string.IsNullOrWhiteSpace(callbackBaseUrl))
            {
                resolvedCallbackUrl = $"{callbackBaseUrl.TrimEnd('/')}/projects/{payload.TenantId}/{payload.ProjectId}/scopes/link-gts";
                if (!string.IsNullOrWhiteSpace(callbackFunctionKey))
                {
                    resolvedCallbackUrl = $"{resolvedCallbackUrl}?code={Uri.EscapeDataString(callbackFunctionKey)}";
                }
            }
            var callbackSecret = _configuration["ScopeSyncCallbackSecret"];
            if (string.IsNullOrWhiteSpace(resolvedCallbackUrl))
            {
                _logger.LogWarning(
                    "Project scope callback URL is not configured. Set ProjectScopeLinkCallbackBaseUrl or ProjectScopeLinkCallbackUrl.");
                return false;
            }
            var orchestrationRunId = Guid.NewGuid().ToString();

            try
            {
                foreach (var scope in addedScopes)
                {
                    var gtsShellRequest = new Dictionary<string, object>
                    {
                        ["GroupTask"] = new object[] { },
                        ["ProjectID"] = payload.ProjectId,
                        ["TenantID"] = payload.TenantId,
                        ["id"] = "00000000-0000-0000-0000-000000000000",
                        ["ScopeID"] = scope.ScopeId,
                        ["ProjectScopeAreaTitle"] = scope.ProjectScopeAreaTitle,
                        ["ProjectScopeArea"] = scope.ProjectScopeArea,
                        ["ProjectScopeAreaEmbeddings"] = scope.ProjectScopeAreaEmbeddings ?? new List<float>(),
                        ["ProjectScopeLinkCallbackUrl"] = resolvedCallbackUrl,
                        ["ProjectScopeLinkSecret"] = callbackSecret,
                        ["OrchestrationRunId"] = orchestrationRunId
                    };

                    var requestJson = JsonConvert.SerializeObject(gtsShellRequest);
                    using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                    {
                        Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
                    };

                    var response = await _httpClient.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        var body = await response.Content.ReadAsStringAsync();
                        _logger.LogWarning(
                            "Scope sync publish failed for ProjectId={ProjectId}, ScopeId={ScopeId}. Status={Status}. Body={Body}",
                            payload.ProjectId,
                            scope.ScopeId,
                            response.StatusCode,
                            body);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scope sync publish exception for ProjectId={ProjectId}", payload.ProjectId);
                return false;
            }
        }
    }
}
