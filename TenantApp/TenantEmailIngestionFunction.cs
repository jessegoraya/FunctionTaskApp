using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Taslow.Shared.Security;
using Taslow.Tenant.DAL.Interface;
using Taslow.Shared.Model;
using Taslow.Tenant.Model;
using Taslow.Tenant.Service.Interface;

namespace Taslow.Tenant.Function
{
    public class TenantEmailIngestionFunction
    {
        private static readonly Regex MessageIdSlashPattern = new(
            @"messages/(?<id>[^/?]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex MessageIdParenPattern = new(
            @"messages\('(?<id>[^']+)'\)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly ITenantEmailIngestionService _emailIngestionService;
        private readonly ITenantEmailIngestionStateRepository _stateRepository;
        private readonly IGraphNotificationValidator _notificationValidator;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TenantEmailIngestionFunction> _logger;

        public TenantEmailIngestionFunction(
            ITenantEmailIngestionService emailIngestionService,
            ITenantEmailIngestionStateRepository stateRepository,
            IGraphNotificationValidator notificationValidator,
            IConfiguration configuration,
            ILogger<TenantEmailIngestionFunction> logger)
        {
            _emailIngestionService = emailIngestionService;
            _stateRepository = stateRepository;
            _notificationValidator = notificationValidator;
            _configuration = configuration;
            _logger = logger;
        }

        [Function("IngestGraphSentEmailEvent")]
        public async Task<HttpResponseData> IngestGraphSentEmailEvent(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "email-ingestion/graph/events")] HttpRequestData req)
        {
            var correlationId = GetCorrelationId(req);
            var query = ParseQuery(req.Url.Query);
            if (query.TryGetValue("validationToken", out var validationToken)
                && !string.IsNullOrWhiteSpace(validationToken))
            {
                return await PlainText(req, HttpStatusCode.OK, validationToken, correlationId);
            }

            try
            {
                var rawBody = await ReadBodyAsync(req);
                if (TryParseGraphNotificationEnvelope(rawBody, out var notifications))
                {
                    return await ProcessGraphNotificationsAsync(
                        req,
                        notifications,
                        correlationId);
                }

                if (!AllowDirectPayloads())
                {
                    throw new TenantApiException(
                        HttpStatusCode.BadRequest,
                        TenantErrorCodes.BadRequest,
                        "A Microsoft Graph notification envelope is required.");
                }

                var requestPayload = DeserializeBody<GraphEmailEventIngestionRequest>(rawBody);
                var intakeResult = await _emailIngestionService.IntakeGraphEventAsync(
                    requestPayload,
                    correlationId,
                    req.FunctionContext.CancellationToken);
                return await Json(req, HttpStatusCode.Accepted, intakeResult.Response, correlationId);
            }
            catch (TenantApiException ex)
            {
                _logger.LogWarning(ex, "Email ingestion API error: {Code}.", ex.Code);
                return await Json(req, ex.StatusCode, new ApiErrorResponse
                {
                    Error = new ApiError
                    {
                        Code = ex.Code,
                        Message = ex.Message,
                        CorrelationId = correlationId,
                        Details = ex.Details
                    }
                }, correlationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled email ingestion API error.");
                return await Json(req, HttpStatusCode.InternalServerError, new ApiErrorResponse
                {
                    Error = new ApiError
                    {
                        Code = TenantErrorCodes.BadRequest,
                        Message = "Unhandled server error.",
                        CorrelationId = correlationId,
                        Details = new List<string>()
                    }
                }, correlationId);
            }
        }

        [Function("GetInternalTenantEmailIngestionEvidence")]
        public async Task<HttpResponseData> GetInternalTenantEmailIngestionEvidence(
            [HttpTrigger(
                AuthorizationLevel.Function,
                "get",
                Route = "internal/email-ingestion/evidence/{idempotencyKey}")] HttpRequestData req,
            string idempotencyKey)
        {
            var correlationId = GetCorrelationId(req);
            if (!WorkloadRequestAuthorizer.IsEmailE2ETestRunnerAuthorized(
                FirstHeader(req, WorkloadRequestAuthorizer.HeaderName)))
            {
                return await Json(
                    req,
                    HttpStatusCode.Unauthorized,
                    new { error = "unauthorized" },
                    correlationId);
            }

            if (!Regex.IsMatch(
                idempotencyKey ?? string.Empty,
                "^[a-f0-9]{64}$",
                RegexOptions.CultureInvariant))
            {
                return await Json(
                    req,
                    HttpStatusCode.BadRequest,
                    new { error = "A valid email idempotency key is required." },
                    correlationId);
            }

            var normalizedIdempotencyKey = idempotencyKey!.ToLowerInvariant();
            var record = await _stateRepository.GetByIdAsync(
                normalizedIdempotencyKey,
                req.FunctionContext.CancellationToken);
            if (record == null)
            {
                return await Json(
                    req,
                    HttpStatusCode.NotFound,
                    new { exists = false, idempotencyKey = normalizedIdempotencyKey },
                    correlationId);
            }

            return await Json(
                req,
                HttpStatusCode.OK,
                new
                {
                    exists = true,
                    idempotencyKey = record.Id,
                    record.Status,
                    record.AgentRunId,
                    record.TaskWriteCount,
                    taskWrites = (record.TaskWrites ?? new List<TenantEmailTaskWriteEvidence>())
                        .Select(task => new
                        {
                            task.IdempotencyKey,
                            task.GroupTaskSetId,
                            task.GroupTaskId,
                            task.ProjectId,
                            task.ScopeId
                        }),
                    record.CreatedAt,
                    record.UpdatedAt,
                    hasError = !string.IsNullOrWhiteSpace(record.LastError),
                    failureCategory = ClassifyFailure(record.LastError),
                    protectedMessageFieldsIncluded = false
                },
                correlationId);
        }

        [Function("ProcessTenantEmailExtractionQueue")]
        public async Task ProcessTenantEmailExtractionQueue(
            [QueueTrigger("%TenantEmailIngestionQueueName%", Connection = "AzureWebJobsStorage")] string payload,
            int dequeueCount,
            FunctionContext context)
        {
            TenantEmailExtractionQueueMessage message;
            try
            {
                message = JsonConvert.DeserializeObject<TenantEmailExtractionQueueMessage>(payload)
                    ?? throw new InvalidOperationException("Queue payload is empty.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Invalid extraction queue payload.");
                throw;
            }

            var correlationId = string.IsNullOrWhiteSpace(message.CorrelationId)
                ? Guid.NewGuid().ToString()
                : message.CorrelationId;
            await _emailIngestionService.ProcessExtractionMessageAsync(
                message,
                dequeueCount,
                correlationId,
                context.CancellationToken);
        }

        private async Task<HttpResponseData> ProcessGraphNotificationsAsync(
            HttpRequestData req,
            List<JObject> notifications,
            string correlationId)
        {
            var results = new List<TenantEmailIngestionResponse>();
            foreach (var notification in notifications)
            {
                if (!TryReadNotification(notification, out var metadata, out var skipReason))
                {
                    _logger.LogWarning(
                        "Skipping unsupported Graph notification. reason={Reason} subscriptionId={SubscriptionId}",
                        skipReason,
                        notification.Value<string>("subscriptionId") ?? string.Empty);
                    continue;
                }

                var route = await _notificationValidator.ValidateAsync(
                    metadata.ClientState,
                    metadata.SubscriptionId,
                    req.FunctionContext.CancellationToken);
                if (route == null)
                {
                    _logger.LogWarning(
                        "Skipping unauthenticated Graph notification. subscriptionId={SubscriptionId}",
                        metadata.SubscriptionId);
                    continue;
                }

                try
                {
                    var intake = await _emailIngestionService.IntakeGraphEventAsync(
                        new GraphEmailEventIngestionRequest
                        {
                            TenantId = route.TenantId,
                            Mailbox = route.Mailbox,
                            Direction = route.Direction,
                            GraphEventId = metadata.GraphEventId,
                            MessageId = metadata.MessageId,
                            SubscriptionId = metadata.SubscriptionId
                        },
                        correlationId,
                        req.FunctionContext.CancellationToken);
                    results.Add(intake.Response);
                }
                catch (TenantApiException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Graph notification intake rejected. tenantId={TenantId} graphEventId={GraphEventId}",
                        route.TenantId,
                        metadata.GraphEventId);
                }
            }

            return await Json(req, HttpStatusCode.Accepted, new
            {
                status = "accepted",
                received = notifications.Count,
                processed = results.Count,
                queued = results.Count(result => result.Enqueued)
            }, correlationId);
        }

        private bool AllowDirectPayloads()
        {
            var value = _configuration["TenantEmailIngestion:AllowDirectPayloads"]
                ?? _configuration["TenantEmailIngestion__AllowDirectPayloads"];
            return bool.TryParse(value, out var enabled) && enabled;
        }

        internal static string ClassifyFailure(string? lastError)
        {
            if (string.IsNullOrWhiteSpace(lastError))
            {
                return "none";
            }

            if (lastError.Contains("Graph", StringComparison.OrdinalIgnoreCase))
            {
                return "graph_hydration";
            }

            if (lastError.Contains("Foundry", StringComparison.OrdinalIgnoreCase))
            {
                return "foundry_invocation";
            }

            if (lastError.Contains("task", StringComparison.OrdinalIgnoreCase)
                || lastError.Contains("Logic App", StringComparison.OrdinalIgnoreCase)
                || lastError.Contains("project context", StringComparison.OrdinalIgnoreCase))
            {
                return "task_write";
            }

            return "unclassified";
        }

        private static bool TryParseGraphNotificationEnvelope(
            string body,
            out List<JObject> notifications)
        {
            notifications = new List<JObject>();
            if (string.IsNullOrWhiteSpace(body))
            {
                return false;
            }

            try
            {
                var value = JToken.Parse(body)["value"] as JArray;
                if (value == null)
                {
                    return false;
                }

                notifications.AddRange(value.OfType<JObject>());
                return notifications.Count > 0;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool TryReadNotification(
            JObject notification,
            out GraphNotificationMetadata metadata,
            out string reason)
        {
            metadata = new GraphNotificationMetadata();
            reason = string.Empty;
            if (!(notification.Value<string>("changeType") ?? string.Empty)
                .Equals("created", StringComparison.OrdinalIgnoreCase))
            {
                reason = "unsupported_change_type";
                return false;
            }

            var subscriptionId = notification.Value<string>("subscriptionId") ?? string.Empty;
            var clientState = notification.Value<string>("clientState") ?? string.Empty;
            var resource = notification.Value<string>("resource") ?? string.Empty;
            var messageId = notification.SelectToken("resourceData.id")?.Value<string>()
                ?? TryExtractMessageId(resource)
                ?? string.Empty;
            if (string.IsNullOrWhiteSpace(subscriptionId)
                || string.IsNullOrWhiteSpace(clientState)
                || string.IsNullOrWhiteSpace(messageId))
            {
                reason = "missing_notification_identity";
                return false;
            }

            metadata = new GraphNotificationMetadata
            {
                ClientState = clientState,
                SubscriptionId = subscriptionId,
                MessageId = messageId,
                GraphEventId = notification.Value<string>("id")
                    ?? $"{subscriptionId}:{messageId}"
            };
            return true;
        }

        private static string? TryExtractMessageId(string resource)
        {
            if (string.IsNullOrWhiteSpace(resource))
            {
                return null;
            }

            var slashMatch = MessageIdSlashPattern.Match(resource);
            if (slashMatch.Success)
            {
                return Uri.UnescapeDataString(slashMatch.Groups["id"].Value);
            }

            var parenMatch = MessageIdParenPattern.Match(resource);
            return parenMatch.Success
                ? Uri.UnescapeDataString(parenMatch.Groups["id"].Value)
                : null;
        }

        private static T DeserializeBody<T>(string body) where T : class, new()
        {
            return string.IsNullOrWhiteSpace(body)
                ? new T()
                : JsonConvert.DeserializeObject<T>(body) ?? new T();
        }

        private static async Task<string> ReadBodyAsync(HttpRequestData req)
        {
            using var reader = new StreamReader(req.Body);
            return await reader.ReadToEndAsync();
        }

        private static async Task<HttpResponseData> Json<T>(
            HttpRequestData req,
            HttpStatusCode statusCode,
            T payload,
            string correlationId)
        {
            var response = req.CreateResponse(statusCode);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            response.Headers.Add("x-correlation-id", correlationId);
            await response.WriteStringAsync(JsonConvert.SerializeObject(payload), Encoding.UTF8);
            return response;
        }

        private static async Task<HttpResponseData> PlainText(
            HttpRequestData req,
            HttpStatusCode statusCode,
            string content,
            string correlationId)
        {
            var response = req.CreateResponse(statusCode);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            response.Headers.Add("x-correlation-id", correlationId);
            await response.WriteStringAsync(content, Encoding.UTF8);
            return response;
        }

        private static string GetCorrelationId(HttpRequestData req)
        {
            if (req.Headers.TryGetValues("x-correlation-id", out var values))
            {
                var incoming = values.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(incoming))
                {
                    return incoming;
                }
            }

            return Guid.NewGuid().ToString();
        }

        private static string? FirstHeader(HttpRequestData req, string name)
        {
            return req.Headers.TryGetValues(name, out var values)
                ? values.FirstOrDefault()
                : null;
        }

        private static Dictionary<string, string> ParseQuery(string queryString)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in (queryString ?? string.Empty)
                .TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split('=', 2);
                var key = WebUtility.UrlDecode(pair[0]) ?? string.Empty;
                var value = pair.Length > 1 ? WebUtility.UrlDecode(pair[1]) ?? string.Empty : string.Empty;
                result[key] = value;
            }

            return result;
        }

        private sealed class GraphNotificationMetadata
        {
            public string ClientState { get; set; } = string.Empty;
            public string SubscriptionId { get; set; } = string.Empty;
            public string MessageId { get; set; } = string.Empty;
            public string GraphEventId { get; set; } = string.Empty;
        }
    }
}
