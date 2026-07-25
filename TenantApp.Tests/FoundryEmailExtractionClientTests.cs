using System.Net;
using System.Text;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Taslow.Shared.Model;
using Taslow.Tenant.Model;
using Taslow.Tenant.Service;
using Xunit;

namespace TenantApp.Tests
{
    public class FoundryEmailExtractionClientTests
    {
        [Fact]
        public async Task InvokeAsync_ShouldUseFoundryIdentityAndParseTasks()
        {
            var handler = new RecordingHandler();
            var credential = new RecordingTokenCredential();
            var client = CreateClient(
                "https://project.services.ai.azure.com/api/projects/test/agents/taslow-email-extraction/endpoint/protocols/invocations?api-version=v1",
                handler,
                credential);

            var response = await client.InvokeAsync(new TenantEmailExtractionQueueMessage
            {
                TenantId = "tenant-1",
                Mailbox = "ahassan@bloomsky.onmicrosoft.com",
                Direction = TenantEmailDirections.Sent,
                GraphEventId = "event-1",
                InternetMessageId = "<message-1@example.com>",
                MessageId = "message-1",
                Subject = "Task",
                BodyText = "Complete the review.",
                SentDateTime = "2026-07-22T12:00:00.0000000+00:00"
            }, "corr-1");

            Assert.Equal("agent-run-1", response.AgentRunId);
            Assert.Single(response.Tasks);
            Assert.Equal("https://ai.azure.com/.default", credential.Scope);
            Assert.Equal("Bearer foundry-token", handler.Authorization);
            Assert.Equal("HostedAgents=V1Preview", handler.FoundryFeatures);
            Assert.Contains(
                "\"sentDateTime\":\"2026-07-22T12:00:00.0000000+00:00\"",
                handler.PayloadJson);
        }

        [Fact]
        public async Task InvokeAsync_ShouldRejectVersionSpecificEndpoint()
        {
            var client = CreateClient(
                "https://project.services.ai.azure.com/api/projects/test/agents/taslow-email-extraction/versions/7/endpoint/protocols/invocations?api-version=v1",
                new RecordingHandler(),
                new RecordingTokenCredential());

            var error = await Assert.ThrowsAsync<TenantEmailIngestionException>(() =>
                client.InvokeAsync(new TenantEmailExtractionQueueMessage(), "corr-1"));

            Assert.False(error.IsTransient);
        }

        [Fact]
        public async Task InvokeAsync_ShouldRejectEndpointWithoutFoundryApiVersion()
        {
            var client = CreateClient(
                "https://project.services.ai.azure.com/api/projects/test/agents/taslow-email-extraction/endpoint/protocols/invocations",
                new RecordingHandler(),
                new RecordingTokenCredential());

            var error = await Assert.ThrowsAsync<TenantEmailIngestionException>(() =>
                client.InvokeAsync(new TenantEmailExtractionQueueMessage(), "corr-1"));

            Assert.False(error.IsTransient);
        }

        private static FoundryEmailExtractionClient CreateClient(
            string endpoint,
            HttpMessageHandler handler,
            TokenCredential credential)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TenantEmailIngestion:FoundryAgentEndpoint"] = endpoint
                })
                .Build();
            return new FoundryEmailExtractionClient(
                new SingleClientFactory(new HttpClient(handler)),
                configuration,
                credential);
        }

        private sealed class RecordingHandler : HttpMessageHandler
        {
            public string? Authorization { get; private set; }
            public string? FoundryFeatures { get; private set; }
            public string PayloadJson { get; private set; } = string.Empty;

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                Authorization = request.Headers.Authorization?.ToString();
                FoundryFeatures = request.Headers.GetValues("Foundry-Features").Single();
                PayloadJson = request.Content!
                    .ReadAsStringAsync(cancellationToken)
                    .GetAwaiter()
                    .GetResult();
                var payload = """
                    {
                      "agentRunId": "agent-run-1",
                      "status": "tasks_ready",
                      "taskCandidateCount": 1,
                      "tasks": [
                        {
                          "sourceTaskId": "task-1",
                          "title": "Complete review",
                          "description": "Complete the review.",
                          "projectId": "project-1",
                          "scopeId": "scope-1",
                          "assigneeEmail": "ahassan@bloomsky.onmicrosoft.com",
                          "assigneeName": "Amina Hassan",
                          "overallConfidence": 0.95,
                          "needsReview": false
                        }
                      ]
                    }
                    """;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                });
            }
        }

        private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => client;
        }

        private sealed class RecordingTokenCredential : TokenCredential
        {
            public string? Scope { get; private set; }

            public override AccessToken GetToken(
                TokenRequestContext requestContext,
                CancellationToken cancellationToken)
            {
                Scope = requestContext.Scopes.Single();
                return new AccessToken("foundry-token", DateTimeOffset.UtcNow.AddHours(1));
            }

            public override ValueTask<AccessToken> GetTokenAsync(
                TokenRequestContext requestContext,
                CancellationToken cancellationToken)
            {
                return ValueTask.FromResult(GetToken(requestContext, cancellationToken));
            }
        }
    }
}
