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
        public string Id { get; set; }

        [JsonProperty("projectName")]
        public string ProjectName { get; set; }

        [JsonProperty("projectDescription")]
        public string ProjectDescription { get; set; }

        [JsonProperty("projectType")]
        public string ProjectType { get; set; }

        [JsonProperty("projectStatus")]
        public string ProjectStatus { get; set; }

        [JsonProperty("tenantId")]
        public string TenantId { get; set; }
     }

}
