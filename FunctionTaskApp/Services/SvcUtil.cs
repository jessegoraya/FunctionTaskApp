using System;
using System.Collections.Generic;
using System.Text;
using CMaaS.Task.Model;
using System.Security.Cryptography;

namespace CMaaS.Task.Service
{
    class SvcUtil
    {
        public string Create(string tenant, string id)
        {
            //Project Documents will create SHA256 beased id from tenant id and project id and Tasks from tenant id and task id
            using (SHA256 _sha256 = SHA256.Create())
            {
                byte[] bytehash = _sha256.ComputeHash(Encoding.UTF8.GetBytes(tenant + id));
                string hashvalue = BitConverter.ToString(bytehash);
                return hashvalue;
            }


        }

        public GroupTask SetNewIDs(GroupTask gt)
        {
            gt.grouptaskid = Guid.NewGuid().ToString();
            gt.createddate = DateTime.Now;
            gt.lastmodifieddate = DateTime.Now;
            if (gt.individualtasksets.Count > 0)
            {
                foreach (IndividualTaskSet its in gt.individualtasksets)
                {
                    if (string.IsNullOrEmpty(its.individualtasksetid) || its.individualtasksetid == "00000000-0000-0000-0000-000000000000")
                    {
                        its.individualtasksetid = Guid.NewGuid().ToString();
                        its.createddate = DateTime.Now;
                    }

                    foreach (IndividualTask it in its.individualtask)
                    {
                        if (string.IsNullOrEmpty(it.individualtaskid) || it.individualtaskid == "00000000-0000-0000-0000-000000000000")
                        {
                            it.individualtaskid = Guid.NewGuid().ToString();
                            it.createddate = DateTime.Now;
                        }
                    }
                }
            }

            return gt;
        }



    public IndividualTaskSet SetNewITSIDs(IndividualTaskSet its)
    {
        {
            its.individualtasksetid = Guid.NewGuid().ToString();
            its.createddate = DateTime.Now;
            foreach (IndividualTask it in its.individualtask)
            {
                if (it.individualtaskid == null)
                {
                    it.individualtaskid = Guid.NewGuid().ToString();
                    it.createddate = DateTime.Now;
                }
            }
            return its;
            }
    }

              

        public IndividualTask SetNewITIDs(IndividualTask it)
        {
            if (string.IsNullOrEmpty(it.individualtaskid) || it.individualtaskid == "00000000-0000-0000-0000-000000000000")
            {
                it.individualtaskid = Guid.NewGuid().ToString();
            }

            return it;
        }

    public IndividualTask SetStandardValues(IndividualTask it)
    {

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
