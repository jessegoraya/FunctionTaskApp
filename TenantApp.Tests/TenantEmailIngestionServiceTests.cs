using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Taslow.Shared.Model;
using Taslow.Tenant.DAL.Interface;
using Taslow.Tenant.Model;
using Taslow.Tenant.Service;
using Taslow.Tenant.Service.Interface;
using Xunit;

namespace TenantApp.Tests
{
    public class TenantEmailIngestionServiceTests
    {
        [Fact]
        public void BuildIdempotencyKey_ShouldBeCaseInsensitive()
        {
            var keyA = TenantEmailIdempotencyKeyBuilder.Build(
                "11111111-1111-4111-8111-111111111111",
                "Jesse@Foray.OnMicrosoft.com",
                "<Message-Id@contoso.com>",
                "SENT");

            var keyB = TenantEmailIdempotencyKeyBuilder.Build(
                "11111111-1111-4111-8111-111111111111",
                "jesse@foray.onmicrosoft.com",
                "<message-id@contoso.com>",
                "sent");

            Assert.Equal(keyA, keyB);
        }

        [Fact]
        public async Task IntakeGraphEventAsync_ShouldQueueMessage_WhenTenantIsActiveAndEnabled()
        {
            var tenant = CreateTenant(enabled: true);
            var tenantRepository = new FakeTenantRepository(tenant);
            var stateRepository = new FakeStateRepository(tryCreateResult: true);
            var queueClient = new FakeQueueClient();
            var extractionClient = new FakeExtractionClient();
            var service = CreateService(tenantRepository, stateRepository, queueClient, extractionClient);

            var request = CreateRequest();
            var result = await service.IntakeGraphEventAsync(request, "corr-1");

            Assert.True(result.Response.Enqueued);
            Assert.Equal(TenantEmailIngestionStatus.Queued, result.Response.Status);
            Assert.NotNull(result.QueueMessage);
            Assert.Single(queueClient.Messages);
            Assert.Equal(request.MessageId, queueClient.Messages[0].MessageId);
            Assert.False(string.IsNullOrWhiteSpace(result.Response.IdempotencyKey));
        }

        [Fact]
        public async Task IntakeGraphEventAsync_ShouldReturnDuplicate_WhenIdempotencyAlreadyExists()
        {
            var tenant = CreateTenant(enabled: true);
            var tenantRepository = new FakeTenantRepository(tenant);
            var stateRepository = new FakeStateRepository(tryCreateResult: false);
            var queueClient = new FakeQueueClient();
            var extractionClient = new FakeExtractionClient();
            var service = CreateService(tenantRepository, stateRepository, queueClient, extractionClient);

            var result = await service.IntakeGraphEventAsync(CreateRequest(), "corr-1");

            Assert.False(result.Response.Enqueued);
            Assert.Equal(TenantEmailIngestionStatus.Duplicate, result.Response.Status);
            Assert.Empty(queueClient.Messages);
        }

        [Fact]
        public async Task ProcessExtractionMessageAsync_ShouldThrowOnceForTransientFailure_ThenMarkFailed()
        {
            var tenant = CreateTenant(enabled: true);
            var tenantRepository = new FakeTenantRepository(tenant);
            var stateRepository = new FakeStateRepository(tryCreateResult: true);
            var queueClient = new FakeQueueClient();
            var extractionClient = new FakeExtractionClient
            {
                Handler = (_, _) => throw new TenantEmailIngestionException("temporary", isTransient: true)
            };

            var service = CreateService(tenantRepository, stateRepository, queueClient, extractionClient);
            var request = CreateRequest();
            var intake = await service.IntakeGraphEventAsync(request, "corr-1");
            var message = intake.QueueMessage!;

            var first = await Record.ExceptionAsync(async () =>
            {
                await service.ProcessExtractionMessageAsync(message, dequeueCount: 1, correlationId: "corr-1");
            });
            Assert.NotNull(first);

            var second = await Record.ExceptionAsync(async () =>
            {
                await service.ProcessExtractionMessageAsync(message, dequeueCount: 2, correlationId: "corr-1");
            });
            Assert.Null(second);

            var state = await stateRepository.GetByIdAsync(message.IdempotencyKey);
            Assert.NotNull(state);
            Assert.Equal(TenantEmailIngestionStatus.Failed, state!.Status);
        }

        [Fact]
        public async Task IntakeGraphEventAsync_ShouldIgnoreWhenFeatureDisabled()
        {
            var tenant = CreateTenant(enabled: false);
            var tenantRepository = new FakeTenantRepository(tenant);
            var stateRepository = new FakeStateRepository(tryCreateResult: true);
            var queueClient = new FakeQueueClient();
            var extractionClient = new FakeExtractionClient();
            var service = CreateService(tenantRepository, stateRepository, queueClient, extractionClient);

            var result = await service.IntakeGraphEventAsync(CreateRequest(), "corr-1");

            Assert.False(result.Response.Enqueued);
            Assert.Equal(TenantEmailIngestionStatus.Ignored, result.Response.Status);
            Assert.Equal("email_ingestion_disabled", result.Response.Reason);
            Assert.Empty(queueClient.Messages);
        }

        private static TenantEmailIngestionService CreateService(
            ITenantRepository tenantRepository,
            ITenantEmailIngestionStateRepository stateRepository,
            ITenantEmailQueueClient queueClient,
            IEmailExtractionClient extractionClient)
        {
            return new TenantEmailIngestionService(
                tenantRepository,
                stateRepository,
                queueClient,
                new FakeGraphMessageClient(),
                extractionClient,
                new FakeTaskWriteClient(),
                NullLogger<TenantEmailIngestionService>.Instance);
        }

        private static GraphEmailEventIngestionRequest CreateRequest()
        {
            return new GraphEmailEventIngestionRequest
            {
                TenantId = "11111111-1111-4111-8111-111111111111",
                Mailbox = "jesse@foray.onmicrosoft.com",
                Direction = TenantEmailDirections.Sent,
                GraphEventId = "graph-event-1",
                InternetMessageId = "<abc@example.com>",
                MessageId = "AAMkAGI2...",
                Subject = "Task assignment",
                BodyText = "Please complete this by Friday."
            };
        }

        private static TenantDocumentDTO CreateTenant(bool enabled)
        {
            return new TenantDocumentDTO
            {
                Id = "11111111-1111-4111-8111-111111111111",
                Tenant = new TenantCoreDTO
                {
                    TenantId = "11111111-1111-4111-8111-111111111111",
                    Status = TenantStatuses.Active,
                    DisplayName = "Foraya",
                    CreatedAt = DateTime.UtcNow.ToString("O"),
                    UpdatedAt = DateTime.UtcNow.ToString("O")
                },
                Administration = new TenantAdministrationPatchRequest
                {
                    Provider = TenantProviders.Microsoft
                },
                EmailIntegration = new TenantEmailIntegrationPatchRequest
                {
                    Graph = new TenantGraphIntegrationDTO
                    {
                        Enabled = true,
                        EmailIngestionEnabled = enabled
                    },
                    MailboxStates = new List<TenantMailboxStateDTO>
                    {
                        new()
                        {
                            MailboxKey = "jesse@foray.onmicrosoft.com",
                            Status = "active"
                        }
                    },
                    SubscriptionRegistry = new List<TenantSubscriptionRegistryItemDTO>()
                }
            };
        }
    }

    internal class FakeTenantRepository : ITenantRepository
    {
        private readonly TenantDocumentDTO _tenant;

        public FakeTenantRepository(TenantDocumentDTO tenant)
        {
            _tenant = tenant;
        }

        public Task<(TenantDocumentDTO Document, string ETag)> CreateAsync(TenantDocumentDTO document, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<(TenantDocumentDTO? Document, string? ETag)> GetByIdAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            if (_tenant.Id.Equals(tenantId, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<(TenantDocumentDTO?, string?)>((_tenant, "\"etag\""));
            }

            return Task.FromResult<(TenantDocumentDTO?, string?)>((null, null));
        }

        public Task<(List<TenantDocumentDTO> Items, string? ContinuationToken)> ListAsync(TenantListQuery query, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<(TenantDocumentDTO Document, string ETag)> ReplaceAsync(TenantDocumentDTO document, string ifMatchETag, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    internal class FakeStateRepository : ITenantEmailIngestionStateRepository
    {
        private readonly bool _tryCreateResult;
        private readonly Dictionary<string, TenantEmailIngestionStateRecord> _records = new();

        public FakeStateRepository(bool tryCreateResult)
        {
            _tryCreateResult = tryCreateResult;
        }

        public Task<TenantEmailIngestionStateRecord?> GetByIdAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        {
            _records.TryGetValue(idempotencyKey, out var record);
            return Task.FromResult(record);
        }

        public Task<bool> TryCreateAsync(TenantEmailIngestionStateRecord record, CancellationToken cancellationToken = default)
        {
            if (!_tryCreateResult)
            {
                return Task.FromResult(false);
            }

            _records[record.Id] = record;
            return Task.FromResult(true);
        }

        public Task UpsertAsync(TenantEmailIngestionStateRecord record, CancellationToken cancellationToken = default)
        {
            _records[record.Id] = record;
            return Task.CompletedTask;
        }
    }

    internal class FakeQueueClient : ITenantEmailQueueClient
    {
        public List<TenantEmailExtractionQueueMessage> Messages { get; } = new();

        public Task EnqueueAsync(TenantEmailExtractionQueueMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    internal class FakeExtractionClient : IEmailExtractionClient
    {
        public Func<TenantEmailExtractionQueueMessage, string, TenantEmailExtractionInvokeResponse>? Handler { get; set; }

        public Task<TenantEmailExtractionInvokeResponse> InvokeAsync(
            TenantEmailExtractionQueueMessage message,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            if (Handler != null)
            {
                return Task.FromResult(Handler(message, correlationId));
            }

            return Task.FromResult(new TenantEmailExtractionInvokeResponse
            {
                AgentRunId = "run-123",
                Status = "tasks_ready",
                TaskCandidateCount = 1
            });
        }
    }

    internal class FakeGraphMessageClient : IMicrosoftGraphMessageClient
    {
        public Task<TenantEmailExtractionQueueMessage> HydrateAsync(
            TenantEmailExtractionQueueMessage message,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(message);
        }
    }

    internal class FakeTaskWriteClient : IEmailTaskWriteClient
    {
        public Task<int> WriteAsync(
            TenantEmailExtractionQueueMessage message,
            TenantEmailExtractionInvokeResponse extraction,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(extraction.Tasks.Count);
        }
    }
}
