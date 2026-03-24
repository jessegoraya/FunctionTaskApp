using Newtonsoft.Json;

namespace Taslow.Shared.Model
{
    public class ProjectScopeGtsLinkResultDTO
    {
        [JsonProperty("linkedCount")]
        public int LinkedCount { get; set; }

        [JsonProperty("noOpCount")]
        public int NoOpCount { get; set; }

        [JsonProperty("project")]
        public ProjectDetailDTO Project { get; set; }
    }
}
