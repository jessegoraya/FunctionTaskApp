using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Taslow.Shared.Model;
using Taslow.Tenant.Model;
using Taslow.Tenant.Service.Interface;

namespace Taslow.Tenant.Service
{
    public class PromptflowEmailExtractionClient : IEmailExtractionClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public PromptflowEmailExtractionClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<TenantEmailExtractionInvokeResponse> InvokeAsync(
            TenantEmailExtractionQueueMessage message,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            var endpoint = _configuration["TenantEmailIngestionPromptflowEndpoint"];
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new TenantEmailIngestionException("TenantEmailIngestionPromptflowEndpoint is missing.", isTransient: false);
            }

            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            {
                throw new TenantEmailIngestionException("TenantEmailIngestionPromptflowEndpoint is invalid.", isTransient: false);
            }

            var requestBody = JsonConvert.SerializeObject(message);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
            };

            var apiKey = _configuration["TenantEmailIngestionPromptflowApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                var bearerToken = apiKey.Trim();
                const string bearerPrefix = "Bearer ";
                if (bearerToken.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    bearerToken = bearerToken.Substring(bearerPrefix.Length).Trim();
                }

                if (!string.IsNullOrWhiteSpace(bearerToken))
                {
                    httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                }
            }

            httpRequest.Headers.TryAddWithoutValidation("x-correlation-id", correlationId);
            httpRequest.Headers.TryAddWithoutValidation("x-graph-event-id", message.GraphEventId);
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var httpClient = _httpClientFactory.CreateClient(nameof(PromptflowEmailExtractionClient));
            HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(httpRequest, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new TenantEmailIngestionException("Promptflow invocation request failed.", isTransient: true, ex);
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var isTransient = IsTransientStatusCode(response.StatusCode);
                var messageText = $"Promptflow invocation failed with status {(int)response.StatusCode}.";
                throw new TenantEmailIngestionException(messageText, isTransient);
            }

            var fallbackCorrelationId =
                TryGetHeaderValue(response.Headers, "x-request-id")
                ?? TryGetHeaderValue(response.Content.Headers, "x-request-id");

            if (string.IsNullOrWhiteSpace(content))
            {
                return new TenantEmailExtractionInvokeResponse
                {
                    PromptflowRunId = fallbackCorrelationId
                };
            }

            var payload = JObject.Parse(content);
            var runId =
                payload.Value<string>("promptflowRunId")
                ?? payload.Value<string>("runId")
                ?? payload.SelectToken("data.promptflowRunId")?.Value<string>()
                ?? payload.SelectToken("data.runId")?.Value<string>()
                ?? fallbackCorrelationId;

            var taskCandidatesToken =
                payload.SelectToken("taskCandidates")
                ?? payload.SelectToken("tasks")
                ?? payload.SelectToken("data.taskCandidates")
                ?? payload.SelectToken("data.tasks");

            int? taskCandidateCount = null;
            if (taskCandidatesToken is JArray taskCandidatesArray)
            {
                taskCandidateCount = taskCandidatesArray.Count;
            }

            return new TenantEmailExtractionInvokeResponse
            {
                PromptflowRunId = runId,
                TaskCandidateCount = taskCandidateCount
            };
        }

        private static bool IsTransientStatusCode(HttpStatusCode statusCode)
        {
            return statusCode == HttpStatusCode.RequestTimeout
                || statusCode == (HttpStatusCode)429
                || (int)statusCode >= 500;
        }

        private static string? TryGetHeaderValue(HttpHeaders headers, string headerName)
        {
            if (!headers.TryGetValues(headerName, out var values))
            {
                return null;
            }

            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return null;
        }
    }
}

