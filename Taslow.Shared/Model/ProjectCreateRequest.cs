using System.Collections.Generic;
using Newtonsoft.Json;

namespace Taslow.Shared.Model
{
    public class ProjectCreateRequest
    {
        [JsonProperty("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonProperty("projectName")]
        public string ProjectName { get; set; } = string.Empty;

        [JsonProperty("projectDescription")]
        public string ProjectDescription { get; set; } = string.Empty;

        [JsonProperty("projectType")]
        public string ProjectType { get; set; } = string.Empty;

        [JsonProperty("marketCode")]
        public string MarketCode { get; set; } = string.Empty;

        [JsonProperty("projectStatus")]
        public string ProjectStatus { get; set; } = string.Empty;

        [JsonProperty("extProjectId")]
        public string ExtProjectId { get; set; } = string.Empty;

        [JsonProperty("members")]
        public List<string> Members { get; set; } = new();

        [JsonProperty("managers")]
        public List<string> Managers { get; set; } = new();

        [JsonProperty("scopes")]
        public List<ProjectScopePatchItem> Scopes { get; set; } = new();
    }
}
