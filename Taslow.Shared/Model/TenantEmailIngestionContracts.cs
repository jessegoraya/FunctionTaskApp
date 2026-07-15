using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Taslow.Shared.Model
{
    public static class TenantEmailDirections
    {
        public const string Sent = "sent";
        public const string Received = "received";

        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            Sent,
            Received
        };
    }

    public static class TenantEmailIngestionStatus
    {
        public const string Queued = "queued";
        public const string Processed = "processed";
        public const string Failed = "failed";
        public const string QueueFailed = "queue_failed";
        public const string Duplicate = "duplicate";
        public const string Ignored = "ignored";
    }

    public class GraphEmailEventIngestionRequest
    {
        [JsonProperty("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonProperty("mailbox")]
        public string Mailbox { get; set; } = string.Empty;

        [JsonProperty("direction")]
        public string Direction { get; set; } = TenantEmailDirections.Sent;

        [JsonProperty("graphEventId")]
        public string GraphEventId { get; set; } = string.Empty;

        [JsonProperty("internetMessageId")]
        public string InternetMessageId { get; set; } = string.Empty;

        [JsonProperty("messageId")]
        public string MessageId { get; set; } = string.Empty;

        [JsonProperty("subject")]
        public string Subject { get; set; } = string.Empty;

        [JsonProperty("bodyText")]
        public string BodyText { get; set; } = string.Empty;
    }

    public class TenantEmailExtractionQueueMessage
    {
        [JsonProperty("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonProperty("mailbox")]
        public string Mailbox { get; set; } = string.Empty;

        [JsonProperty("direction")]
        public string Direction { get; set; } = TenantEmailDirections.Sent;

        [JsonProperty("graphEventId")]
        public string GraphEventId { get; set; } = string.Empty;

        [JsonProperty("internetMessageId")]
        public string InternetMessageId { get; set; } = string.Empty;

        [JsonProperty("messageId")]
        public string MessageId { get; set; } = string.Empty;

        [JsonProperty("subject")]
        public string Subject { get; set; } = string.Empty;

        [JsonProperty("bodyText")]
        public string BodyText { get; set; } = string.Empty;

        [JsonProperty("idempotencyKey")]
        public string IdempotencyKey { get; set; } = string.Empty;

        [JsonProperty("correlationId")]
        public string CorrelationId { get; set; } = string.Empty;
    }

    public class TenantEmailIngestionResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; } = TenantEmailIngestionStatus.Queued;

        [JsonProperty("reason")]
        public string? Reason { get; set; }

        [JsonProperty("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonProperty("mailbox")]
        public string Mailbox { get; set; } = string.Empty;

        [JsonProperty("graphEventId")]
        public string GraphEventId { get; set; } = string.Empty;

        [JsonProperty("idempotencyKey")]
        public string IdempotencyKey { get; set; } = string.Empty;

        [JsonProperty("enqueued")]
        public bool Enqueued { get; set; }
    }

    public class TenantEmailExtractionInvokeResponse
    {
        [JsonProperty("promptflowRunId")]
        public string? PromptflowRunId { get; set; }

        [JsonProperty("taskCandidateCount")]
        public int? TaskCandidateCount { get; set; }
    }
}
