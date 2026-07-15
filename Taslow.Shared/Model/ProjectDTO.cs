using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Taslow.Shared.Model
{
    public class ProjectDTO
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

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

        [JsonProperty("clientDomains")]
        public List<string> ClientDomains { get; set; } = new();

        [JsonProperty("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonProperty("associatedManagers")]
        public List<ProjectPersonDTO> AssociatedManagers { get; set; } = new();

        [JsonProperty("ProjectScopes")]
        public List<ProjectScopeDTO> ProjectScopes { get; set; } = new();

        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public List<ProjectScopeDTO> Scopes
        {
            get => ProjectScopes;
            set => ProjectScopes = value ?? new();
        }
     }

}
