using System.Collections.Generic;
using Newtonsoft.Json;

namespace Taslow.Shared.Model
{
    public class ProjectDetailDTO : ProjectDTO
    {
        [JsonProperty("extProjectId")]
        public string ExtProjectId { get; set; } = string.Empty;

        [JsonProperty("associatedPeople")]
        public List<ProjectPersonDTO> AssociatedPeople { get; set; } = new();

    }
}
