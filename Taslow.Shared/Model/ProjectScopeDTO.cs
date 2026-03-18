using System.Collections.Generic;
using Newtonsoft.Json;

namespace Taslow.Shared.Model
{
    public class ProjectScopeDTO
    {
        [JsonProperty("scopeId")]
        public string ScopeId { get; set; }

        [JsonProperty("projectScopeArea")]
        public string ProjectScopeArea { get; set; }

        [JsonProperty("projectScopeAreaEmbeddings")]
        public List<float> ProjectScopeAreaEmbeddings { get; set; } = new();
    }
}
