using System.Collections.Generic;
using Newtonsoft.Json;

namespace Taslow.Shared.Model
{
    public class ProjectScopeGtsLinkRequest
    {
        [JsonProperty("mappings")]
        public List<ProjectScopeGtsLinkItem> Mappings { get; set; } = new();
    }

    public class ProjectScopeGtsLinkItem
    {
        [JsonProperty("scopeId")]
        public string ScopeId { get; set; } = string.Empty;

        [JsonProperty("groupTaskSetId")]
        public string GroupTaskSetId { get; set; } = string.Empty;

        [JsonProperty("orchestrationRunId")]
        public string OrchestrationRunId { get; set; } = string.Empty;
    }
}
