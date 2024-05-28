using System;
using System.Collections.Generic;
using System.Text;
using CMaaS.Task.Model;
using System.Text;
using System.Security.Cryptography;

namespace FunctionTaskApp.Services
{
    namespace CMaaS.Task.Service
    {
        class SvcUtil
        {
            public string Create(string tenant, int processtype)
            {
                using (SHA256 _sha256 = SHA256.Create())
                {
                    byte[] bytehash = _sha256.ComputeHash(Encoding.UTF8.GetBytes(tenant + processtype));
                    string hashvalue = BitConverter.ToString(bytehash);
                    return hashvalue;
                }


            }

            public GroupTask SetNewIDs(GroupTask gt)
            {
                gt.grouptaskid = Guid.NewGuid();
                if (gt.individualtasksets.Count > 0)
                {
                    foreach (IndividualTaskSet its in gt.individualtasksets)
                    {
                        if (its.individualtasksetid == null || its.individualtasksetid == Guid.Empty)
                        {
                            its.individualtasksetid = Guid.NewGuid();
                        }

                        foreach (IndividualTask it in its.individualtask)
                        {
                            if (it.individualtaskid == null || it.individualtaskid == Guid.Empty)
                            {
                                it.individualtaskid = Guid.NewGuid();
                            }
                        }
                    }
                }




                return gt;
            } 



            public IndividualTaskSet SetNewITSIDs (IndividualTaskSet its)
            {                {                
                    its.individualtasksetid = Guid.NewGuid();
                 foreach (IndividualTask it in its.individualtask)
                    {
                        if (it.individualtaskid == null || it.individualtaskid == Guid.Empty)
                        {
                            it.individualtaskid = Guid.NewGuid();
                        }
                    }
                }

                return its;
            }

            public IndividualTask SetNewITIDs(IndividualTask it)
            {
                if (it.individualtaskid == null || it.individualtaskid == Guid.Empty)
                {
                    it.individualtaskid = Guid.NewGuid();
                }

                return it;
            }

            public IndividualTask SetStandardValues(IndividualTask it)
            {
                /*
                 *		"individualtaskid": "5854a419-b26b-470b-b162-382415bb6a7a", 
						"individualtaskstatus" : "", 
						"individualtasktitle": "Test Individual Task Title",
						"individualtasktype": "",
						"individualtaskdescription": "",
						"individualtasknotes": "", 
						"priority" : "Normal", 
						"assignedperson": "", 
						"associatedrole": "", 
						"previouslysent": 0, 
						"individualtaskassigneddate": "0001-01-01T00:00:00",
						"individualtaskduedate": "0001-01-01T00:00:00",
						"cancelleddated": "0001-01-01T00:00:00",
						"individualtaskapprovaldecision": "",
						"individualtaskcompleteddate": "0001-01-01T00:00:00",
						"createdby": "", 
						"createddate": "0001-01-01T00:00:00"

                        - Individual Task Title = “Facilitate assignments of” + Group Task Title + task 
                        - Individual Task Type = Facilitating Individual Task 
                        - Individual Task Description = Group Task Description 
                        - Assigned To: Stakeholder Group Name – Facilitators 
                        - Associated Role :Stakeholder Group Name – Facilitators 
                        - Individual Task Due Date: Set to the same date as the creation date of the Group Task 
                        - Individual Task Status: Set to Open 
                        - % Complete: 0% 
                        - Worksite/Administrative Link – Set to the home page of the Worksite/ Administrative site page. For worksite this will be the worksite under which this Individual Task is aligned 
                        - Worksite/Administrative Group Task Link – Set to the home page of the Worksite/Administrative Group Task page where this sits under the Worksite/Administrative
                 */

                if (it.individualtaskstatus == "")
                {
                    it.individualtaskstatus = "Open";
                }

                if (it.priority == "")
                {
                    it.priority = "Normal";
                }

                if (it.createddate == DateTime.Parse("0001-01-01T00:00:00"))
                {
                    it.createddate = DateTime.UtcNow;
                }

                return it;
            }
        }
    }

}
