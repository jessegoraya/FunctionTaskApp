using System;

namespace Taslow.Task.Model
{
    public class MoveIndividualTaskDTO
    {
        public string individualtaskid { get; set; }

        public string sourceprojectid { get; set; }
        public string sourcegrouptaskid { get; set; }
        public string sourceindividualtasksetid { get; set; }

        public string targetprojectid { get; set; }
        public string targetgrouptaskid { get; set; }
        public string targetindividualtasksetid { get; set; }

        public string updatedby { get; set; }
    }
}
