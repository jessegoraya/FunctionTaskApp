using Newtonsoft.Json;
using Taslow.Shared.Model;

namespace Taslow.Tenant.Model
{
    public class TenantEmailIngestionStateRecord
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonProperty("mailbox")]
        public string Mailbox { get; set; } = string.Empty;

        [JsonProperty("direction")]
        public string Direction { get; set; } = string.Empty;

        [JsonProperty("graphEventId")]
        public string GraphEventId { get; set; } = string.Empty;

        [JsonProperty("internetMessageId")]
        public string InternetMessageId { get; set; } = string.Empty;

        [JsonProperty("messageId")]
        public string MessageId { get; set; } = string.Empty;

        [JsonProperty("subscriptionId")]
        public string SubscriptionId { get; set; } = string.Empty;

        [JsonProperty("status")]
        public string Status { get; set; } = string.Empty;

        [JsonProperty("agentRunId")]
        public string? AgentRunId { get; set; }

        [JsonProperty("taskWriteCount")]
        public int TaskWriteCount { get; set; }

        [JsonProperty("taskWrites")]
        public List<TenantEmailTaskWriteEvidence> TaskWrites { get; set; } = new();

        [JsonProperty("lastError")]
        public string? LastError { get; set; }

        [JsonProperty("createdAt")]
        public string CreatedAt { get; set; } = string.Empty;

        [JsonProperty("updatedAt")]
        public string UpdatedAt { get; set; } = string.Empty;
    }
}
