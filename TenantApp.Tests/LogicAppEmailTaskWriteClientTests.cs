using System.Net;
using System.Text;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Taslow.Shared.Model;
using Taslow.Tenant.Model;
using Taslow.Tenant.Service;
using Xunit;

namespace TenantApp.Tests
{
    public class LogicAppEmailTaskWriteClientTests
    {
        [Fact]
        public async Task WriteAsync_ShouldUseManagedIdentityAndWriteEligibleTask()
        {
            var handler = new RecordingHandler();
            var client = CreateClient(handler);

            var written = await client.WriteAsync(
                CreateMessage(),
                CreateExtraction("assignee@bloomsky.onmicrosoft.com"),
                "corr-1");

            Assert.Equal(1, written);
            Assert.Equal("Bearer project-token", handler.ProjectAuthorization);
            Assert.NotNull(handler.LogicAppPayload);
            Assert.Equal("tenant-1", handler.LogicAppPayload!.Value<string>("TenantID"));
            Assert.Equal("gts-1", handler.LogicAppPayload.Value<string>("id"));
            Assert.Single((JArray)handler.LogicAppPayload["grouptask"]!);
        }

        [Fact]
        public async Task WriteAsync_ShouldRejectAssigneeOutsideSelectedProject()
        {
            var handler = new RecordingHandler();
            var client = CreateClient(handler);

            var error = await Assert.ThrowsAsync<TenantEmailIngestionException>(() =>
                client.WriteAsync(
                    CreateMessage(),
                    CreateExtraction("cross-tenant@example.com"),
                    "corr-1"));

            Assert.False(error.IsTransient);
            Assert.Null(handler.LogicAppPayload);
        }

        [Fact]
        public async Task WriteAsync_ShouldSkipDeterministicDuplicateOnRetry()
        {
            var handler = new RecordingHandler();
            var client = CreateClient(handler);
            var message = CreateMessage();
            var extraction = CreateExtraction("assignee@bloomsky.onmicrosoft.com");

            Assert.Equal(1, await client.WriteAsync(message, extraction, "corr-1"));
            handler.ExistingGroupTaskId = handler.LogicAppPayload!
                .SelectToken("grouptask[0].GroupTaskID")!
                .Value<string>();

            Assert.Equal(0, await client.WriteAsync(message, extraction, "corr-2"));
            Assert.Equal(1, handler.LogicAppRequestCount);
        }

        private static LogicAppEmailTaskWriteClient CreateClient(RecordingHandler handler)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TenantEmailIngestion:ProjectServiceEndpoint"] = "https://apim-test.azure-api.net/FunctionProjectApp",
                    ["TenantEmailIngestion:TaskServiceEndpoint"] = "https://apim-test.azure-api.net/FunctionTaskApp",
                    ["TenantEmailIngestion:LogicAppEndpoint"] = "https://eastus.logic.azure.com/workflows/workflow/triggers/request/paths/invoke?sig=secret",
                    ["TenantEmailIngestion:MinimumTaskWriteConfidence"] = "0.80"
                })
                .Build();
            return new LogicAppEmailTaskWriteClient(
                new SingleClientFactory(new HttpClient(handler)),
                configuration,
                new FakeTokenCredential());
        }

        private static TenantEmailExtractionQueueMessage CreateMessage()
        {
            return new TenantEmailExtractionQueueMessage
            {
                TenantId = "tenant-1",
                Mailbox = "ahassan@bloomsky.onmicrosoft.com",
                GraphEventId = "event-1",
                InternetMessageId = "<message-1@bloomsky.onmicrosoft.com>",
                MessageId = "message-1",
                IdempotencyKey = "ingestion-1",
                From = new TenantEmailParticipant
                {
                    Email = "sender@bloomsky.onmicrosoft.com",
                    Name = "Sender"
                }
            };
        }

        private static TenantEmailExtractionInvokeResponse CreateExtraction(string assignee)
        {
            return new TenantEmailExtractionInvokeResponse
            {
                AgentRunId = "agent-run-1",
                Status = "tasks_ready",
                TaskCandidateCount = 1,
                Tasks = new List<TenantExtractedTaskAssignment>
                {
                    new()
                    {
                        SourceTaskId = "task-1",
                        Title = "Complete readiness review",
                        Description = "Review the readiness checklist.",
                        ProjectId = "project-1",
                        ScopeId = "scope-1",
                        AssigneeEmail = assignee,
                        AssigneeName = "Assignee",
                        OverallConfidence = 0.95
                    }
                }
            };
        }

        private sealed class RecordingHandler : HttpMessageHandler
        {
            public string? ProjectAuthorization { get; private set; }
            public JObject? LogicAppPayload { get; private set; }
            public string? ExistingGroupTaskId { get; set; }
            public int LogicAppRequestCount { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                if (request.RequestUri!.AbsolutePath.Contains(
                    "/FunctionProjectApp/",
                    StringComparison.OrdinalIgnoreCase))
                {
                    ProjectAuthorization = request.Headers.Authorization?.ToString();
                    return Json(HttpStatusCode.OK, new
                    {
                        projects = new[]
                        {
                            new
                            {
                                projectId = "project-1",
                                scopes = new[]
                                {
                                    new
                                    {
                                        scopeId = "scope-1",
                                        scopeTitle = "Readiness",
                                        scopeDescription = "Readiness review",
                                        groupTaskSetId = "gts-1"
                                    }
                                },
                                associatedPeople = new[]
                                {
                                    new
                                    {
                                        email = "assignee@bloomsky.onmicrosoft.com",
                                        displayName = "Assignee"
                                    }
                                }
                            }
                        }
                    });
                }

                if (request.RequestUri.AbsolutePath.Contains(
                    "/FunctionTaskApp/",
                    StringComparison.OrdinalIgnoreCase))
                {
                    var tasks = string.IsNullOrWhiteSpace(ExistingGroupTaskId)
                        ? Array.Empty<object>()
                        : new object[] { new { GroupTaskID = ExistingGroupTaskId } };
                    return Json(HttpStatusCode.OK, new { GroupTask = tasks });
                }

                LogicAppPayload = JObject.Parse(
                    await request.Content!.ReadAsStringAsync(cancellationToken));
                LogicAppRequestCount++;
                return Json(HttpStatusCode.OK, new { status = "succeeded" });
            }

            private static HttpResponseMessage Json(HttpStatusCode status, object payload)
            {
                return new HttpResponseMessage(status)
                {
                    Content = new StringContent(
                        Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                        Encoding.UTF8,
                        "application/json")
                };
            }
        }

        private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => client;
        }

        private sealed class FakeTokenCredential : TokenCredential
        {
            public override AccessToken GetToken(
                TokenRequestContext requestContext,
                CancellationToken cancellationToken)
            {
                return new AccessToken("project-token", DateTimeOffset.UtcNow.AddHours(1));
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
