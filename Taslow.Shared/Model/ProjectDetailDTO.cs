using System.Collections.Generic;
using Newtonsoft.Json;

namespace Taslow.Shared.Model
{
    public class ProjectDetailDTO : ProjectDTO
    {
        [JsonProperty("extProjectId")]
        public string ExtProjectId { get; set; }

        [JsonProperty("associatedPeople")]
        public List<ProjectPersonDTO> AssociatedPeople { get; set; } = new();

        [JsonProperty("associatedManagers")]
        public List<ProjectPersonDTO> AssociatedManagers { get; set; } = new();

        [JsonProperty("scopes")]
        public List<ProjectScopeDTO> Scopes { get; set; } = new();
    }
}
