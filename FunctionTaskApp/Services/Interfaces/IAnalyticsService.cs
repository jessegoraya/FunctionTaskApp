using System.Collections.Generic;
using System.Threading.Tasks;
using Taslow.Shared.Model;

namespace Taslow.Task.Service.Interface
{
    public interface IAnalyticsService
    {
        Task<AnalyticsPortfolioResponse> GetPortfolioAsync(
            string tenantId,
            string userEmail,
            IReadOnlyCollection<string> roles,
            IReadOnlyCollection<string> leaderMarketCodes,
            IReadOnlyCollection<string> marketCodeFilter);

        Task<AnalyticsProjectTypeResponse> GetProjectTypeAsync(
            string tenantId,
            string projectType,
            string userEmail,
            IReadOnlyCollection<string> roles,
            IReadOnlyCollection<string> leaderMarketCodes,
            IReadOnlyCollection<string> marketCodeFilter);

        Task<AnalyticsProjectHierarchyResponse> GetProjectHierarchyAsync(
            string tenantId,
            string projectId,
            string userEmail,
            IReadOnlyCollection<string> roles,
            IReadOnlyCollection<string> leaderMarketCodes);
    }
}
