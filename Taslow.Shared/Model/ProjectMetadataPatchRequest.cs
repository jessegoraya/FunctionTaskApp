using Newtonsoft.Json;

namespace Taslow.Shared.Model
{
    public class ProjectMetadataPatchRequest
    {
        [JsonProperty("projectName")]
        public string ProjectName { get; set; }

        [JsonProperty("projectDescription")]
        public string ProjectDescription { get; set; }

        [JsonProperty("projectType")]
        public string ProjectType { get; set; }

        [JsonProperty("projectStatus")]
        public string ProjectStatus { get; set; }

        [JsonProperty("extProjectId")]
        public string ExtProjectId { get; set; }
    }
}
