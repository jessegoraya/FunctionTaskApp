using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Taslow.Shared.Model;
using Taslow.Task.Client.Interface;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace Taslow.Task.Client
{
    public class ProjectServiceClient : IProjectServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ProjectServiceClient> _log;

        public ProjectServiceClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<Dictionary<string, ProjectDTO>>
            GetProjectsAsync(List<string> projectIds, string tenantId)
        {
            var request = new ProjectBatchRequest
            {
                TenantId = tenantId,
                ProjectIds = projectIds
            };

            var response = await _httpClient.PostAsJsonAsync(
                "/api/projects/batch",
                request);

            response.EnsureSuccessStatusCode();

            var result = await response.Content
                .ReadFromJsonAsync<ProjectBatchResponse>();

            return result?.Projects ?? new();
        }

        public async Task<List<string>> GetProjectIdsForManagerAsync(
        string tenantId,
        string manager)
        {
            try
            {
                var response = await _httpClient.GetAsync(
                    $"/api/projects/managed/{tenantId}/{manager}");

                response.EnsureSuccessStatusCode();

                return await response.Content
                    .ReadFromJsonAsync<List<string>>() ?? new();
            }
            catch (Exception ex)
            {
                _log.LogError(
                    ex,
                    "Error retrieving project associations. TenantId={TenantId}, Manager={Manager}",
                    tenantId,
                    manager
                );

                return new List<string>();
            }
        }


    }
}
