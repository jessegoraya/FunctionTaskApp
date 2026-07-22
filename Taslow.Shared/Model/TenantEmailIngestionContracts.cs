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

        [JsonProperty("subscriptionId")]
        public string SubscriptionId { get; set; } = string.Empty;

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

        [JsonProperty("subscriptionId")]
        public string SubscriptionId { get; set; } = string.Empty;

        [JsonProperty("subject")]
        public string Subject { get; set; } = string.Empty;

        [JsonProperty("bodyText")]
        public string BodyText { get; set; } = string.Empty;

        [JsonProperty("sentDateTime")]
        public string? SentDateTime { get; set; }

        [JsonProperty("from")]
        public TenantEmailParticipant? From { get; set; }

        [JsonProperty("to")]
        public List<TenantEmailParticipant> To { get; set; } = new();

        [JsonProperty("cc")]
        public List<TenantEmailParticipant> Cc { get; set; } = new();

        [JsonProperty("bcc")]
        public List<TenantEmailParticipant> Bcc { get; set; } = new();

        [JsonProperty("conversationId")]
        public string? ConversationId { get; set; }

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
        [JsonProperty("agentRunId")]
        public string? AgentRunId { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; } = string.Empty;

        [JsonProperty("taskCandidateCount")]
        public int? TaskCandidateCount { get; set; }

        [JsonProperty("tasks")]
        public List<TenantExtractedTaskAssignment> Tasks { get; set; } = new();
    }

    public class TenantEmailParticipant
    {
        [JsonProperty("email")]
        public string Email { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class TenantExtractedTaskAssignment
    {
        [JsonProperty("sourceTaskId")]
        public string SourceTaskId { get; set; } = string.Empty;

        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("projectId")]
        public string ProjectId { get; set; } = string.Empty;

        [JsonProperty("scopeId")]
        public string? ScopeId { get; set; }

        [JsonProperty("assigneeEmail")]
        public string AssigneeEmail { get; set; } = string.Empty;

        [JsonProperty("assigneeName")]
        public string AssigneeName { get; set; } = string.Empty;

        [JsonProperty("dueDate")]
        public string? DueDate { get; set; }

        [JsonProperty("overallConfidence")]
        public double OverallConfidence { get; set; }

        [JsonProperty("needsReview")]
        public bool NeedsReview { get; set; }
    }
}
