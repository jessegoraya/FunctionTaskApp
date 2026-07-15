using Taslow.Shared.Model;

namespace Taslow.Tenant.Model
{
    public class TenantEmailIngestionIntakeResult
    {
        public TenantEmailIngestionResponse Response { get; set; } = new();

        public TenantEmailExtractionQueueMessage? QueueMessage { get; set; }
    }
}
