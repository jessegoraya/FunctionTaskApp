using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Taslow.Shared.Model
{
    public class ProjectScopeSyncPayload
    {
        [JsonProperty("tenantId")]
        public string TenantId { get; set; }

        [JsonProperty("projectId")]
        public string ProjectId { get; set; }

        [JsonProperty("added")]
        public List<ProjectScopeSyncItem> Added { get; set; } = new();

        [JsonProperty("updated")]
        public List<ProjectScopeSyncItem> Updated { get; set; } = new();

        [JsonProperty("removed")]
        public List<ProjectScopeSyncItem> Removed { get; set; } = new();

        [JsonProperty("generatedAtUtc")]
        public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public class ProjectScopeSyncItem
    {
        [JsonProperty("scopeId")]
        public string ScopeId { get; set; }

        [JsonProperty("projectScopeArea")]
        public string ProjectScopeArea { get; set; }

        [JsonProperty("projectScopeAreaEmbeddings")]
        public List<float> ProjectScopeAreaEmbeddings { get; set; } = new();

        [JsonProperty("groupTaskSetId")]
        public string GroupTaskSetId { get; set; }
    }
}
