using System.Collections.Generic;

using Newtonsoft.Json;

namespace Taslow.Shared.Model
{
    public class ProjectScopeLinkRequest
    {
        [JsonProperty("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonProperty("projectId")]
        public string ProjectId { get; set; } = string.Empty;

        [JsonProperty("mappings")]
        public List<ProjectScopeLinkMapping> Mappings { get; set; } = new List<ProjectScopeLinkMapping>();
    }

    public class ProjectScopeLinkMapping
    {
        [JsonProperty("scopeId")]
        public string ScopeId { get; set; } = string.Empty;

        [JsonProperty("groupTaskSetId")]
        public string GroupTaskSetId { get; set; } = string.Empty;

        [JsonProperty("orchestrationRunId")]
        public string OrchestrationRunId { get; set; } = string.Empty;
    }

    public class ProjectScopeLinkResponse
    {
        [JsonProperty("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonProperty("projectId")]
        public string ProjectId { get; set; } = string.Empty;

        [JsonProperty("updated")]
        public bool Updated { get; set; }

        [JsonProperty("mappings")]
        public List<ProjectScopeLinkResult> Mappings { get; set; } = new List<ProjectScopeLinkResult>();
    }

    public class ProjectScopeLinkResult
    {
        [JsonProperty("scopeId")]
        public string ScopeId { get; set; } = string.Empty;

        [JsonProperty("groupTaskSetId")]
        public string GroupTaskSetId { get; set; } = string.Empty;

        [JsonProperty("status")]
        public string Status { get; set; } = string.Empty;

        [JsonProperty("orchestrationRunId")]
        public string OrchestrationRunId { get; set; } = string.Empty;
    }
}
