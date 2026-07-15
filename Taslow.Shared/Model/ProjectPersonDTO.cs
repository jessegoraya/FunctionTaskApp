using Newtonsoft.Json;

namespace Taslow.Shared.Model
{
    public class ProjectPersonDTO
    {
        [JsonProperty(PropertyName = "AssociatedPersonID")]
        public Guid AssociatedPersonId { get; set; }
        [JsonProperty(PropertyName = "PersonName")]
        public string PersonName { get; set; } = string.Empty;
        [JsonProperty(PropertyName = "PersonAliases")]
        public string PersonAliases { get; set; } = string.Empty;
        [JsonProperty(PropertyName = "PersonEmail")]
        public string PersonEmail { get; set; } = string.Empty;
        // "Manager" or "Person"
        [JsonProperty(PropertyName = "Role")]
        public string Role { get; set; } = string.Empty;
    }

}
