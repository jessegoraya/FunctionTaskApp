using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Taslow.Shared.Model;
using Taslow.Tenant.DAL.Interface;
using Taslow.Tenant.Model;
using Taslow.Tenant.Service.Interface;

namespace Taslow.Tenant.Service
{
    public class TenantEmailIngestionService : ITenantEmailIngestionService
    {
        private readonly ITenantRepository _tenantRepository;
        private readonly ITenantEmailIngestionStateRepository _stateRepository;
        private readonly ITenantEmailQueueClient _queueClient;
        private readonly IEmailExtractionClient _extractionClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TenantEmailIngestionService> _logger;

        public TenantEmailIngestionService(
            ITenantRepository tenantRepository,
            ITenantEmailIngestionStateRepository stateRepository,
            ITenantEmailQueueClient queueClient,
            IEmailExtractionClient extractionClient,
            IConfiguration configuration,
            ILogger<TenantEmailIngestionService> logger)
        {
            _tenantRepository = tenantRepository;
            _stateRepository = stateRepository;
            _queueClient = queueClient;
            _extractionClient = extractionClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<TenantEmailIngestionIntakeResult> IntakeGraphEventAsync(
            GraphEmailEventIngestionRequest request,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request);

            var pilotMailbox = (_configuration["TenantEmailIngestionPilotMailbox"] ?? "jesse@foray.onmicrosoft.com").Trim();
            if (!request.Mailbox.Equals(pilotMailbox, StringComparison.OrdinalIgnoreCase))
            {
                return BuildIgnored(request, "mailbox_not_in_pilot_scope");
            }

            var (tenant, _) = await _tenantRepository.GetByIdAsync(request.TenantId, cancellationToken);
            if (tenant == null)
            {
                throw new TenantApiException(HttpStatusCode.NotFound, TenantErrorCodes.NotFound, "Tenant not found.");
            }

            if (!TenantStatuses.Active.Equals(tenant.Tenant.Status, StringComparison.OrdinalIgnoreCase))
            {
                return BuildIgnored(request, "tenant_not_active");
            }

            var emailIngestionEnabled = tenant.EmailIntegration.Graph?.EmailIngestionEnabled == true;
            if (!emailIngestionEnabled)
            {
                return BuildIgnored(request, "email_ingestion_disabled");
            }

            var idempotencyKey = TenantEmailIdempotencyKeyBuilder.Build(
                request.TenantId,
                request.Mailbox,
                request.InternetMessageId,
                request.Direction);

            var now = DateTime.UtcNow.ToString("O");
            var stateRecord = new TenantEmailIngestionStateRecord
            {
                Id = idempotencyKey,
                TenantId = request.TenantId,
                Mailbox = request.Mailbox,
                Direction = request.Direction,
                GraphEventId = request.GraphEventId,
                InternetMessageId = request.InternetMessageId,
                MessageId = request.MessageId,
                Status = TenantEmailIngestionStatus.Queued,
                CreatedAt = now,
                UpdatedAt = now
            };

            var created = await _stateRepository.TryCreateAsync(stateRecord, cancellationToken);
            if (!created)
            {
                return new TenantEmailIngestionIntakeResult
                {
                    Response = new TenantEmailIngestionResponse
                    {
                        Status = TenantEmailIngestionStatus.Duplicate,
                        Reason = "duplicate_idempotency_key",
                        TenantId = request.TenantId,
                        Mailbox = request.Mailbox,
                        GraphEventId = request.GraphEventId,
                        IdempotencyKey = idempotencyKey,
                        Enqueued = false
                    }
                };
            }

            var queueMessage = new TenantEmailExtractionQueueMessage
            {
                TenantId = request.TenantId,
                Mailbox = request.Mailbox,
                Direction = request.Direction,
                GraphEventId = request.GraphEventId,
                InternetMessageId = request.InternetMessageId,
                MessageId = request.MessageId,
                Subject = request.Subject,
                BodyText = request.BodyText,
                IdempotencyKey = idempotencyKey,
                CorrelationId = correlationId
            };

            try
            {
                await _queueClient.EnqueueAsync(queueMessage, cancellationToken);
            }
            catch (Exception ex)
            {
                stateRecord.Status = TenantEmailIngestionStatus.QueueFailed;
                stateRecord.LastError = ex.Message;
                stateRecord.UpdatedAt = DateTime.UtcNow.ToString("O");
                await _stateRepository.UpsertAsync(stateRecord, cancellationToken);

                throw new TenantApiException(
                    HttpStatusCode.InternalServerError,
                    TenantErrorCodes.BadRequest,
                    "Failed to enqueue extraction request.");
            }

            return new TenantEmailIngestionIntakeResult
            {
                QueueMessage = queueMessage,
                Response = new TenantEmailIngestionResponse
                {
                    Status = TenantEmailIngestionStatus.Queued,
                    TenantId = request.TenantId,
                    Mailbox = request.Mailbox,
                    GraphEventId = request.GraphEventId,
                    IdempotencyKey = idempotencyKey,
                    Enqueued = true
                }
            };
        }

        public async Task ProcessExtractionMessageAsync(
            TenantEmailExtractionQueueMessage message,
            int dequeueCount,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var extractionResult = await _extractionClient.InvokeAsync(message, correlationId, cancellationToken);
                await UpdateStateRecordAsync(
                    message.IdempotencyKey,
                    TenantEmailIngestionStatus.Processed,
                    extractionResult.PromptflowRunId,
                    null,
                    cancellationToken);

                _logger.LogInformation(
                    "Email extraction processed. tenantId={TenantId} mailbox={Mailbox} graphEventId={GraphEventId} promptflowRunId={PromptflowRunId}",
                    message.TenantId,
                    message.Mailbox,
                    message.GraphEventId,
                    extractionResult.PromptflowRunId ?? string.Empty);
            }
            catch (TenantEmailIngestionException ex)
            {
                if (ex.IsTransient && dequeueCount <= 1)
                {
                    _logger.LogWarning(
                        ex,
                        "Transient extraction failure. graphEventId={GraphEventId} dequeueCount={DequeueCount}. Retrying once.",
                        message.GraphEventId,
                        dequeueCount);
                    throw;
                }

                await UpdateStateRecordAsync(
                    message.IdempotencyKey,
                    TenantEmailIngestionStatus.Failed,
                    null,
                    ex.Message,
                    cancellationToken);

                _logger.LogError(
                    ex,
                    "Extraction failed after retry policy. graphEventId={GraphEventId} dequeueCount={DequeueCount}",
                    message.GraphEventId,
                    dequeueCount);
            }
            catch (Exception ex)
            {
                if (dequeueCount <= 1)
                {
                    _logger.LogWarning(
                        ex,
                        "Unhandled extraction failure. graphEventId={GraphEventId} dequeueCount={DequeueCount}. Retrying once.",
                        message.GraphEventId,
                        dequeueCount);
                    throw;
                }

                await UpdateStateRecordAsync(
                    message.IdempotencyKey,
                    TenantEmailIngestionStatus.Failed,
                    null,
                    ex.Message,
                    cancellationToken);

                _logger.LogError(
                    ex,
                    "Unhandled extraction failure after retry policy. graphEventId={GraphEventId} dequeueCount={DequeueCount}",
                    message.GraphEventId,
                    dequeueCount);
            }
        }

        private async Task UpdateStateRecordAsync(
            string idempotencyKey,
            string status,
            string? promptflowRunId,
            string? lastError,
            CancellationToken cancellationToken)
        {
            var record = await _stateRepository.GetByIdAsync(idempotencyKey, cancellationToken);
            if (record == null)
            {
                return;
            }

            record.Status = status;
            record.PromptflowRunId = promptflowRunId ?? record.PromptflowRunId;
            record.LastError = lastError;
            record.UpdatedAt = DateTime.UtcNow.ToString("O");
            await _stateRepository.UpsertAsync(record, cancellationToken);
        }

        private static TenantEmailIngestionIntakeResult BuildIgnored(GraphEmailEventIngestionRequest request, string reason)
        {
            return new TenantEmailIngestionIntakeResult
            {
                Response = new TenantEmailIngestionResponse
                {
                    Status = TenantEmailIngestionStatus.Ignored,
                    Reason = reason,
                    TenantId = request.TenantId,
                    Mailbox = request.Mailbox,
                    GraphEventId = request.GraphEventId,
                    Enqueued = false
                }
            };
        }

        private static void ValidateRequest(GraphEmailEventIngestionRequest request)
        {
            if (request == null)
            {
                throw new TenantApiException(HttpStatusCode.BadRequest, TenantErrorCodes.BadRequest, "Request payload is required.");
            }

            if (string.IsNullOrWhiteSpace(request.TenantId))
            {
                throw new TenantApiException(HttpStatusCode.BadRequest, TenantErrorCodes.ValidationFailed, "tenantId is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Mailbox))
            {
                throw new TenantApiException(HttpStatusCode.BadRequest, TenantErrorCodes.ValidationFailed, "mailbox is required.");
            }

            if (string.IsNullOrWhiteSpace(request.GraphEventId))
            {
                throw new TenantApiException(HttpStatusCode.BadRequest, TenantErrorCodes.ValidationFailed, "graphEventId is required.");
            }

            if (string.IsNullOrWhiteSpace(request.MessageId))
            {
                throw new TenantApiException(HttpStatusCode.BadRequest, TenantErrorCodes.ValidationFailed, "messageId is required.");
            }

            if (string.IsNullOrWhiteSpace(request.InternetMessageId))
            {
                throw new TenantApiException(HttpStatusCode.BadRequest, TenantErrorCodes.ValidationFailed, "internetMessageId is required.");
            }

            if (request.Subject == null)
            {
                throw new TenantApiException(HttpStatusCode.BadRequest, TenantErrorCodes.ValidationFailed, "subject is required.");
            }

            if (request.BodyText == null)
            {
                throw new TenantApiException(HttpStatusCode.BadRequest, TenantErrorCodes.ValidationFailed, "bodyText is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Direction))
            {
                throw new TenantApiException(HttpStatusCode.BadRequest, TenantErrorCodes.ValidationFailed, "direction is required.");
            }

            if (!request.Direction.Equals(TenantEmailDirections.Sent, StringComparison.OrdinalIgnoreCase))
            {
                throw new TenantApiException(HttpStatusCode.BadRequest, TenantErrorCodes.ValidationFailed, "Only direction=sent is supported in V1.");
            }
        }
    }
}
