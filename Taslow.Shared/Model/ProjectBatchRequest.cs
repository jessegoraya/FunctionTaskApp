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
        public string TenantId { get; set; }
        public List<string> ProjectIds { get; set; } = new();
    }
}
