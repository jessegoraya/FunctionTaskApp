using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Taslow.Shared.Model;
using Taslow.Task.Client.Interface;
using Microsoft.Extensions.Logging;

namespace Taslow.Task.Client
{
    public class ProjectServiceClient : IProjectServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ProjectServiceClient> _log;

        public ProjectServiceClient(HttpClient httpClient, ILogger<ProjectServiceClient> log)
        {
            _httpClient = httpClient;
            _log = log;

            if (_httpClient.BaseAddress is { } baseAddress
                && !baseAddress.AbsoluteUri.EndsWith("/", StringComparison.Ordinal))
            {
                _httpClient.BaseAddress = new Uri($"{baseAddress.AbsoluteUri}/");
            }
        }

        public async Task<List<ProjectDTO>>
            GetProjectsAsync(List<string> projectIds, string tenantId, string accessToken)
        {
            try
            {
                var request = new ProjectBatchRequest
                {
                    TenantId = tenantId,
                    ProjectIds = projectIds
                };

                using var message = new HttpRequestMessage(HttpMethod.Post, "projects/batch")
                {
                    Content = JsonContent.Create(request)
                };
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                var response = await _httpClient.SendAsync(message);

                response.EnsureSuccessStatusCode();

                var result = await response.Content
                    .ReadFromJsonAsync<ProjectBatchResponse>();

                return result?.Projects ?? new();
            }
            catch (Exception ex)
            {
                _log.LogError(
                    ex,
                    "Error retrieving project batch. TenantId={TenantId}, ProjectCount={ProjectCount}",
                    tenantId,
                    projectIds?.Count ?? 0);

                return new List<ProjectDTO>();
            }
        }

        public async Task<List<string>> GetProjectIdsForManagerAsync(
            string tenantId,
            string manager,
            string accessToken)
        {
            try
            {
                using var message = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"projects/managed/{tenantId}/{Uri.EscapeDataString(manager)}");
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                var response = await _httpClient.SendAsync(message);

                EnsureTenantAccess(response);

                return await response.Content
                    .ReadFromJsonAsync<List<string>>() ?? new();
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (KeyNotFoundException)
            {
                throw;
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

        public async Task<List<ProjectDTO>> GetActiveProjectsAsync(string tenantId, string accessToken)
        {
            try
            {
                using var message = new HttpRequestMessage(HttpMethod.Get, $"projects/active/{tenantId}");
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                var response = await _httpClient.SendAsync(message);
                EnsureTenantAccess(response);

                return await response.Content.ReadFromJsonAsync<List<ProjectDTO>>() ?? new();
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error retrieving active projects. TenantId={TenantId}", tenantId);
                return new List<ProjectDTO>();
            }
        }

        private static void EnsureTenantAccess(HttpResponseMessage response)
        {
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new UnauthorizedAccessException("Project access was denied.");
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new KeyNotFoundException("Tenant or Project was not found.");
            }

            response.EnsureSuccessStatusCode();
        }

    }
}
