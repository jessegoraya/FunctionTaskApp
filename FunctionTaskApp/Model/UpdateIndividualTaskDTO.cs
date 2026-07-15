using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Taslow.Task.Model
{
    public class UpdateIndividualTaskDTO
    {
        public string individualtaskid { get; set; }

        public string individualtasktitle { get; set; }

        public string individualtaskdescription { get; set; }

        public string assignedperson { get; set; }
        public string status { get; set; }

        public DateTime? individualtaskduedate { get; set; }
    }
}
