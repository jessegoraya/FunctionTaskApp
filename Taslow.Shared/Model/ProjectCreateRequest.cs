using System.Collections.Generic;
using Newtonsoft.Json;

namespace Taslow.Shared.Model
{
    public class ProjectCreateRequest
    {
        [JsonProperty("tenantId")]
        public string TenantId { get; set; }

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

        [JsonProperty("members")]
        public List<string> Members { get; set; } = new();

        [JsonProperty("managers")]
        public List<string> Managers { get; set; } = new();

        [JsonProperty("scopes")]
        public List<ProjectScopePatchItem> Scopes { get; set; } = new();
    }
}
