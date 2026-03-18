using System.Collections.Generic;
using Newtonsoft.Json;

namespace Taslow.Shared.Model
{
    public class ProjectScopePatchResultDTO
    {
        [JsonProperty("project")]
        public ProjectDetailDTO Project { get; set; }

        [JsonProperty("scopeSync")]
        public ProjectScopeSyncPayload ScopeSync { get; set; }
    }
}
