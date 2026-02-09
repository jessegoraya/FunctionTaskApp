using Newtonsoft.Json;

namespace Taslow.Shared.Model
{
    public class ProjectPersonDTO
    {
        [JsonProperty(PropertyName = "AssociatedPersonID")]
        public Guid AssociatedPersonId { get; set; }
        [JsonProperty(PropertyName = "PersonName")]
        public string PersonName { get; set; }
        [JsonProperty(PropertyName = "PersonAliases")]
        public string PersonAliases { get; set; }
        [JsonProperty(PropertyName = "PersonEmail")]
        public string PersonEmail { get; set; }
        // "Manager" or "Person"
        [JsonProperty(PropertyName = "Role")]
        public string Role { get; set; }
    }

}