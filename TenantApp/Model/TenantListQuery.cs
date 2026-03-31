namespace Taslow.Tenant.Model
{
    public class TenantListQuery
    {
        public string? Status { get; set; }
        public string? Search { get; set; }
        public int PageSize { get; set; } = 25;
        public string? ContinuationToken { get; set; }
    }
}
