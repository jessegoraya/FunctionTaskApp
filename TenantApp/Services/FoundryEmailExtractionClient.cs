using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Taslow.Shared.Model;
using Taslow.Tenant.Model;
using Taslow.Tenant.Service.Interface;

namespace Taslow.Tenant.Service
{
    public class FoundryEmailExtractionClient : IEmailExtractionClient
    {
        private static readonly TokenRequestContext FoundryTokenContext = new(
            new[] { "https://ai.azure.com/.default" });

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly TokenCredential _credential;

        public FoundryEmailExtractionClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            TokenCredential credential)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _credential = credential;
        }

        public async Task<TenantEmailExtractionInvokeResponse> InvokeAsync(
            TenantEmailExtractionQueueMessage message,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            var endpoint = _configuration["TenantEmailIngestion:FoundryAgentEndpoint"]
                ?? _configuration["TenantEmailIngestion__FoundryAgentEndpoint"];
            if (!TryValidateEndpoint(endpoint, out var endpointUri))
            {
                throw new TenantEmailIngestionException(
                    "The governed Foundry agent endpoint is missing or invalid.",
                    isTransient: false);
            }

            AccessToken accessToken;
            try
            {
                accessToken = await _credential.GetTokenAsync(FoundryTokenContext, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new TenantEmailIngestionException(
                    "Foundry managed-identity authentication failed.",
                    isTransient: true,
                    ex);
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, endpointUri)
            {
                Content = new StringContent(
                    JsonConvert.SerializeObject(message),
                    Encoding.UTF8,
                    "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.TryAddWithoutValidation("Foundry-Features", "HostedAgents=V1Preview");
            request.Headers.TryAddWithoutValidation("x-correlation-id", correlationId);
            request.Headers.TryAddWithoutValidation("x-graph-event-id", message.GraphEventId);

            var client = _httpClientFactory.CreateClient(nameof(FoundryEmailExtractionClient));
            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                throw new TenantEmailIngestionException(
                    "Foundry agent invocation failed.",
                    isTransient: true,
                    ex);
            }

            using (response)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    throw new TenantEmailIngestionException(
                        $"Foundry agent invocation failed with status {(int)response.StatusCode}.",
                        IsTransient(response.StatusCode));
                }

                TenantEmailExtractionInvokeResponse? result;
                try
                {
                    result = JsonConvert.DeserializeObject<TenantEmailExtractionInvokeResponse>(content);
                }
                catch (JsonException ex)
                {
                    throw new TenantEmailIngestionException(
                        "Foundry agent response was invalid.",
                        isTransient: false,
                        ex);
                }

                if (result == null || string.IsNullOrWhiteSpace(result.AgentRunId))
                {
                    throw new TenantEmailIngestionException(
                        "Foundry agent response omitted agentRunId.",
                        isTransient: false);
                }

                if (result.Status.Equals("retryable", StringComparison.OrdinalIgnoreCase))
                {
                    throw new TenantEmailIngestionException(
                        "Foundry agent reported a retryable dependency failure.",
                        isTransient: true);
                }

                return result;
            }
        }

        private static bool TryValidateEndpoint(string? value, out Uri endpoint)
        {
            endpoint = null!;
            if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed)
                || parsed.Scheme != Uri.UriSchemeHttps
                || !parsed.Host.EndsWith(".services.ai.azure.com", StringComparison.OrdinalIgnoreCase)
                || !parsed.AbsolutePath.Contains("/agents/", StringComparison.OrdinalIgnoreCase)
                || parsed.AbsolutePath.Contains("/versions/", StringComparison.OrdinalIgnoreCase)
                || !parsed.AbsolutePath.EndsWith(
                    "/endpoint/protocols/invocations",
                    StringComparison.OrdinalIgnoreCase)
                || !parsed.Query.Equals("?api-version=v1", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            endpoint = parsed;
            return true;
        }

        private static bool IsTransient(HttpStatusCode statusCode)
        {
            return statusCode == HttpStatusCode.RequestTimeout
                || statusCode == (HttpStatusCode)429
                || (int)statusCode >= 500;
        }
    }
}
