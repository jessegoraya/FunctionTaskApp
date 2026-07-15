using System.Collections.Generic;
using Newtonsoft.Json;

namespace Taslow.Shared.Model
{
    public class ProjectScopeDTO
    {
        [JsonProperty("scopeId")]
        public string ScopeId { get; set; } = string.Empty;

        [JsonProperty("projectScopeAreaTitle")]
        public string ProjectScopeAreaTitle { get; set; } = string.Empty;

        [JsonProperty("projectScopeArea")]
        public string ProjectScopeArea { get; set; } = string.Empty;

        [JsonProperty("projectScopeAreaEmbeddings")]
        public List<float> ProjectScopeAreaEmbeddings { get; set; } = new();

        [JsonProperty("groupTaskSetId")]
        public string GroupTaskSetId { get; set; } = string.Empty;
    }
}
