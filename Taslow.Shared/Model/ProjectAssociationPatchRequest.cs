using System.Collections.Generic;
using Newtonsoft.Json;

namespace Taslow.Shared.Model
{
    public class ProjectAssociationPatchRequest
    {
        [JsonProperty("members")]
        public List<string> Members { get; set; } = new();

        [JsonProperty("managers")]
        public List<string> Managers { get; set; } = new();
    }
}
