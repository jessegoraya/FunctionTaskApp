using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Taslow.Shared.Model;

namespace Taslow.Shared.Model
{
    public class ProjectBatchResponse
    {
        public Dictionary<string, ProjectDTO> Projects { get; set; } = new();
    }
}
