using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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

            try
            {
                var response = await _httpClient.PostAsJsonAsync(endpoint, payload);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning(
                        "Scope sync publish failed for ProjectId={ProjectId}. Status={Status}. Body={Body}",
                        payload.ProjectId,
                        response.StatusCode,
                        body);
                    return false;
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
