using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Taslow.Shared.Model
{
    public class ProjectBatchRequest
    {
        [JsonProperty("tenantId")]
        public string TenantId { get; set; }

        [JsonProperty("projectIds")]
        public List<string> ProjectIds { get; set; } = new();
    }
}
