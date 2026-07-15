using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Taslow.Shared.Model;
using Taslow.Tenant.Model;
using Taslow.Tenant.Service.Interface;

namespace Taslow.Tenant.Function
{
    public class TenantEmailIngestionFunction
    {
        private static readonly Regex MessageIdSlashPattern = new(@"messages/(?<id>[^/?]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex MessageIdParenPattern = new(@"messages\('(?<id>[^']+)'\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly ITenantEmailIngestionService _emailIngestionService;
        private readonly ILogger<TenantEmailIngestionFunction> _logger;

        public TenantEmailIngestionFunction(
            ITenantEmailIngestionService emailIngestionService,
            ILogger<TenantEmailIngestionFunction> logger)
        {
            _emailIngestionService = emailIngestionService;
            _logger = logger;
        }

        [Function("IngestGraphSentEmailEvent")]
        public async Task<HttpResponseData> IngestGraphSentEmailEvent(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "email-ingestion/graph/events")] HttpRequestData req)
        {
            var correlationId = GetCorrelationId(req);

            var query = ParseQuery(req.Url.Query);
            if (query.TryGetValue("validationToken", out var validationToken) && !string.IsNullOrWhiteSpace(validationToken))
            {
                return await PlainText(req, HttpStatusCode.OK, validationToken, correlationId);
            }

            try
            {
                var rawBody = await ReadBodyAsync(req);

                if (TryParseGraphNotificationEnvelope(rawBody, out var notifications))
                {
                    var results = new List<TenantEmailIngestionResponse>();

                    foreach (var notification in notifications)
                    {
                        if (!TryBuildRequestFromNotification(notification, out var request, out var skipReason))
                        {
                            _logger.LogWarning(
                                "Skipping unsupported Graph notification. reason={Reason} notification={Notification}",
                                skipReason,
                                notification.ToString(Formatting.None));
                            continue;
                        }

                        try
                        {
                            var intake = await _emailIngestionService.IntakeGraphEventAsync(
                                request,
                                correlationId,
                                req.FunctionContext.CancellationToken);

                            results.Add(intake.Response);
                        }
                        catch (TenantApiException ex)
                        {
                            _logger.LogWarning(
                                ex,
                                "Graph notification intake rejected. tenantId={TenantId} mailbox={Mailbox} graphEventId={GraphEventId}",
                                request.TenantId,
                                request.Mailbox,
                                request.GraphEventId);
                        }
                    }

                    var summary = new
                    {
                        status = "accepted",
                        received = notifications.Count,
                        processed = results.Count,
                        queued = results.Count(r => r.Enqueued)
                    };

                    return await Json(req, HttpStatusCode.Accepted, summary, correlationId);
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
                _logger.LogWarning(ex, "Email ingestion API error: {Code} - {Message}", ex.Code, ex.Message);
                var payload = new ApiErrorResponse
                {
                    Error = new ApiError
                    {
                        Code = ex.Code,
                        Message = ex.Message,
                        CorrelationId = correlationId,
                        Details = ex.Details
                    }
                };

                return await Json(req, ex.StatusCode, payload, correlationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled email ingestion API error.");
                var payload = new ApiErrorResponse
                {
                    Error = new ApiError
                    {
                        Code = TenantErrorCodes.BadRequest,
                        Message = "Unhandled server error.",
                        CorrelationId = correlationId,
                        Details = new List<string>()
                    }
                };

                return await Json(req, HttpStatusCode.InternalServerError, payload, correlationId);
            }
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
                return;
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

        private static bool TryParseGraphNotificationEnvelope(string body, out List<JObject> notifications)
        {
            notifications = new List<JObject>();
            if (string.IsNullOrWhiteSpace(body))
            {
                return false;
            }

            JToken token;
            try
            {
                token = JToken.Parse(body);
            }
            catch
            {
                return false;
            }

            var valueArray = token["value"] as JArray;
            if (valueArray == null)
            {
                return false;
            }

            foreach (var item in valueArray)
            {
                if (item is JObject obj)
                {
                    notifications.Add(obj);
                }
            }

            return notifications.Count > 0;
        }

        private static bool TryBuildRequestFromNotification(
            JObject notification,
            out GraphEmailEventIngestionRequest request,
            out string reason)
        {
            request = new GraphEmailEventIngestionRequest();
            reason = string.Empty;

            var changeType = notification.Value<string>("changeType") ?? string.Empty;
            if (!changeType.Equals("created", StringComparison.OrdinalIgnoreCase))
            {
                reason = "unsupported_change_type";
                return false;
            }

            var clientState = notification.Value<string>("clientState") ?? string.Empty;
            if (!TryParseClientState(clientState, out var tenantId, out var mailbox, out var direction))
            {
                reason = "invalid_client_state";
                return false;
            }

            var resource = notification.Value<string>("resource") ?? string.Empty;
            var messageId = notification.SelectToken("resourceData.id")?.Value<string>()
                ?? TryExtractMessageId(resource);

            if (string.IsNullOrWhiteSpace(messageId))
            {
                reason = "missing_message_id";
                return false;
            }

            var internetMessageId = notification.SelectToken("resourceData.internetMessageId")?.Value<string>()
                ?? messageId;

            var subject = notification.SelectToken("resourceData.subject")?.Value<string>()
                ?? string.Empty;

            var bodyText = notification.SelectToken("resourceData.bodyPreview")?.Value<string>()
                ?? notification.SelectToken("resourceData.body.content")?.Value<string>()
                ?? string.Empty;

            var subscriptionId = notification.Value<string>("subscriptionId") ?? "sub";
            var graphEventId = notification.Value<string>("id")
                ?? $"{subscriptionId}:{messageId}:{DateTime.UtcNow.Ticks}";

            request = new GraphEmailEventIngestionRequest
            {
                TenantId = tenantId,
                Mailbox = mailbox,
                Direction = string.IsNullOrWhiteSpace(direction) ? TenantEmailDirections.Sent : direction,
                GraphEventId = graphEventId,
                InternetMessageId = internetMessageId,
                MessageId = messageId,
                Subject = subject,
                BodyText = bodyText
            };

            return true;
        }

        private static bool TryParseClientState(string clientState, out string tenantId, out string mailbox, out string direction)
        {
            tenantId = string.Empty;
            mailbox = string.Empty;
            direction = TenantEmailDirections.Sent;

            if (string.IsNullOrWhiteSpace(clientState))
            {
                return false;
            }

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var trimmed = clientState.Trim();
            if (trimmed.StartsWith("{", StringComparison.Ordinal))
            {
                try
                {
                    var token = JObject.Parse(trimmed);
                    foreach (var property in token.Properties())
                    {
                        map[property.Name] = property.Value?.ToString() ?? string.Empty;
                    }
                }
                catch
                {
                    return false;
                }
            }
            else
            {
                var parts = trimmed.Split(new[] { ';', '|', ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    var kv = part.Split(new[] { '=', ':' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (kv.Length == 2)
                    {
                        map[kv[0].Trim()] = kv[1].Trim();
                    }
                }
            }

            map.TryGetValue("tenantId", out var tenantIdValue);
            tenantId = tenantIdValue ?? string.Empty;
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                map.TryGetValue("tenant_id", out var altTenantId);
                tenantId = altTenantId ?? string.Empty;
            }

            map.TryGetValue("mailbox", out var mailboxValue);
            mailbox = mailboxValue ?? string.Empty;
            if (string.IsNullOrWhiteSpace(mailbox))
            {
                map.TryGetValue("mail", out var altMailbox);
                mailbox = altMailbox ?? string.Empty;
            }

            if (map.TryGetValue("direction", out var parsedDirection) && !string.IsNullOrWhiteSpace(parsedDirection))
            {
                direction = parsedDirection;
            }

            return !string.IsNullOrWhiteSpace(tenantId) && !string.IsNullOrWhiteSpace(mailbox);
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
            if (parenMatch.Success)
            {
                return Uri.UnescapeDataString(parenMatch.Groups["id"].Value);
            }

            return null;
        }

        private static T DeserializeBody<T>(string body) where T : class, new()
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return new T();
            }

            return JsonConvert.DeserializeObject<T>(body) ?? new T();
        }

        private static async Task<string> ReadBodyAsync(HttpRequestData req)
        {
            using var reader = new StreamReader(req.Body);
            return await reader.ReadToEndAsync();
        }

        private static async Task<HttpResponseData> Json<T>(HttpRequestData req, HttpStatusCode statusCode, T payload, string correlationId)
        {
            var response = req.CreateResponse(statusCode);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            response.Headers.Add("x-correlation-id", correlationId);
            var json = JsonConvert.SerializeObject(payload);
            await response.WriteStringAsync(json, Encoding.UTF8);
            return response;
        }

        private static async Task<HttpResponseData> PlainText(HttpRequestData req, HttpStatusCode statusCode, string content, string correlationId)
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

        private static Dictionary<string, string> ParseQuery(string queryString)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(queryString))
            {
                return result;
            }

            var trimmed = queryString.TrimStart('?');
            var parts = trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 0)
                {
                    continue;
                }

                var key = WebUtility.UrlDecode(kv[0]) ?? string.Empty;
                var value = kv.Length > 1 ? (WebUtility.UrlDecode(kv[1]) ?? string.Empty) : string.Empty;
                result[key] = value;
            }

            return result;
        }
    }
}



