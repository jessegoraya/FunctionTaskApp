using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Taslow.Shared.Model
{
    public static class AnalyticsHealthStatuses
    {
        public const string Healthy = "healthy";
        public const string NeedsAttention = "needs_attention";
        public const string AtRisk = "at_risk";
        public const string NoData = "no_data";
    }

    public class AnalyticsPortfolioResponse
    {
        [JsonProperty("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonProperty("asOfUtc")]
        public string AsOfUtc { get; set; } = string.Empty;

        [JsonProperty("summary")]
        public AnalyticsHealthDistributionDTO Summary { get; set; } = new();

        [JsonProperty("byProjectType")]
        public List<AnalyticsProjectTypeHealthDTO> ByProjectType { get; set; } = new();
    }

    public class AnalyticsProjectTypeResponse
    {
        [JsonProperty("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonProperty("projectType")]
        public string ProjectType { get; set; } = string.Empty;

        [JsonProperty("healthStatus")]
        public string HealthStatus { get; set; } = AnalyticsHealthStatuses.NoData;

        [JsonProperty("summary")]
        public AnalyticsHealthDistributionDTO Summary { get; set; } = new();

        [JsonProperty("byMarketCode")]
        public List<AnalyticsMarketDistributionDTO> ByMarketCode { get; set; } = new();

        [JsonProperty("projects")]
        public List<AnalyticsProjectHealthDTO> Projects { get; set; } = new();

        [JsonProperty("projectsNeedingAttention")]
        public List<AnalyticsProjectAttentionDTO> ProjectsNeedingAttention { get; set; } = new();
    }

    public class AnalyticsProjectHierarchyResponse
    {
        [JsonProperty("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonProperty("project")]
        public AnalyticsProjectHealthDTO Project { get; set; } = new();

        [JsonProperty("nodes")]
        public List<AnalyticsHierarchyNodeDTO> Nodes { get; set; } = new();

        [JsonProperty("exceptions")]
        public List<AnalyticsHierarchyExceptionDTO> Exceptions { get; set; } = new();
    }

    public class AnalyticsHealthDistributionDTO
    {
        [JsonProperty("projectCount")]
        public int ProjectCount { get; set; }

        [JsonProperty("healthyProjectCount")]
        public int HealthyProjectCount { get; set; }

        [JsonProperty("needsAttentionProjectCount")]
        public int NeedsAttentionProjectCount { get; set; }

        [JsonProperty("atRiskProjectCount")]
        public int AtRiskProjectCount { get; set; }

        [JsonProperty("noDataProjectCount")]
        public int NoDataProjectCount { get; set; }
    }

    public class AnalyticsProjectTypeHealthDTO : AnalyticsHealthDistributionDTO
    {
        [JsonProperty("projectType")]
        public string ProjectType { get; set; } = string.Empty;

        [JsonProperty("healthStatus")]
        public string HealthStatus { get; set; } = AnalyticsHealthStatuses.NoData;

        [JsonProperty("overdueIndividualTaskCount")]
        public int OverdueIndividualTaskCount { get; set; }

        [JsonProperty("oldestOverdueIndividualTaskAgeDays")]
        public int? OldestOverdueIndividualTaskAgeDays { get; set; }
    }

    public class AnalyticsMarketDistributionDTO
    {
        [JsonProperty("marketCode")]
        public string MarketCode { get; set; } = string.Empty;

        [JsonProperty("projectCount")]
        public int ProjectCount { get; set; }

        [JsonProperty("atRiskProjectCount")]
        public int AtRiskProjectCount { get; set; }
    }

    public class AnalyticsProjectHealthDTO
    {
        [JsonProperty("projectId")]
        public string ProjectId { get; set; } = string.Empty;

        [JsonProperty("projectName")]
        public string ProjectName { get; set; } = string.Empty;

        [JsonProperty("projectType")]
        public string ProjectType { get; set; } = string.Empty;

        [JsonProperty("marketCode")]
        public string MarketCode { get; set; } = string.Empty;

        [JsonProperty("managerEmails")]
        public List<string> ManagerEmails { get; set; } = new();

        [JsonProperty("healthStatus")]
        public string HealthStatus { get; set; } = AnalyticsHealthStatuses.NoData;

        [JsonProperty("scopeCount")]
        public int ScopeCount { get; set; }

        [JsonProperty("groupTaskCount")]
        public int GroupTaskCount { get; set; }

        [JsonProperty("openIndividualTaskCount")]
        public int OpenIndividualTaskCount { get; set; }

        [JsonProperty("overdueIndividualTaskCount")]
        public int OverdueIndividualTaskCount { get; set; }

        [JsonProperty("dueSoonIndividualTaskCount")]
        public int DueSoonIndividualTaskCount { get; set; }

        [JsonProperty("oldestOpenIndividualTaskAgeDays")]
        public int? OldestOpenIndividualTaskAgeDays { get; set; }

        [JsonProperty("oldestOverdueIndividualTaskAgeDays")]
        public int? OldestOverdueIndividualTaskAgeDays { get; set; }

        [JsonProperty("lastTaskActivityUtc")]
        public string? LastTaskActivityUtc { get; set; }
    }

    public class AnalyticsProjectAttentionDTO
    {
        [JsonProperty("projectId")]
        public string ProjectId { get; set; } = string.Empty;

        [JsonProperty("projectName")]
        public string ProjectName { get; set; } = string.Empty;

        [JsonProperty("marketCode")]
        public string MarketCode { get; set; } = string.Empty;

        [JsonProperty("healthStatus")]
        public string HealthStatus { get; set; } = string.Empty;

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;
    }

    public class AnalyticsHierarchyNodeDTO
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("parentId")]
        public string? ParentId { get; set; }

        [JsonProperty("level")]
        public string Level { get; set; } = string.Empty;

        [JsonProperty("depth")]
        public int Depth { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty("assignee")]
        public string? Assignee { get; set; }

        [JsonProperty("dueDateUtc")]
        public string? DueDateUtc { get; set; }

        [JsonProperty("openCount")]
        public int OpenCount { get; set; }

        [JsonProperty("overdueCount")]
        public int OverdueCount { get; set; }

        [JsonProperty("dueSoonCount")]
        public int DueSoonCount { get; set; }

        [JsonProperty("oldestOpenAgeDays")]
        public int? OldestOpenAgeDays { get; set; }

        [JsonProperty("lastActivityUtc")]
        public string? LastActivityUtc { get; set; }

        [JsonProperty("hasChildren")]
        public bool HasChildren { get; set; }
    }

    public class AnalyticsHierarchyExceptionDTO
    {
        [JsonProperty("individualTaskId")]
        public string IndividualTaskId { get; set; } = string.Empty;

        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty("assignedPerson")]
        public string AssignedPerson { get; set; } = string.Empty;

        [JsonProperty("dueDateUtc")]
        public string DueDateUtc { get; set; } = string.Empty;

        [JsonProperty("ageDays")]
        public int AgeDays { get; set; }

        [JsonProperty("hierarchyPath")]
        public string HierarchyPath { get; set; } = string.Empty;
    }
}
