using Newtonsoft.Json;

namespace Taslow.Shared.Model
{
    public class ProjectDetailDTO : ProjectDTO
    {
        [JsonProperty("extProjectId")]
        public string ExtProjectId { get; set; } = string.Empty;

    }
}
