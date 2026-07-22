namespace Taslow.Tenant.Model
{
    public class GraphNotificationRoute
    {
        public string TenantId { get; set; } = string.Empty;

        public string Mailbox { get; set; } = string.Empty;

        public string Direction { get; set; } = string.Empty;
    }
}
