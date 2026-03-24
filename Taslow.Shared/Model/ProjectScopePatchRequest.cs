using System.Collections.Generic;
using Newtonsoft.Json;

namespace Taslow.Shared.Model
{
    public class ProjectScopePatchRequest
    {
        [JsonProperty("scopes")]
        public List<ProjectScopePatchItem> Scopes { get; set; } = new();
    }

    public class ProjectScopePatchItem
    {
        [JsonProperty("scopeId")]
        public string ScopeId { get; set; }

        [JsonProperty("projectScopeAreaTitle")]
        public string ProjectScopeAreaTitle { get; set; }

        [JsonProperty("projectScopeArea")]
        public string ProjectScopeArea { get; set; }

        [JsonProperty("projectScopeAreaEmbeddings")]
        public List<float> ProjectScopeAreaEmbeddings { get; set; } = new();
    }
}
