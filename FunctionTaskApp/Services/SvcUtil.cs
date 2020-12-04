using System;
using System.Collections.Generic;
using System.Text;
using CMaaS.Task.Model;

namespace FunctionTaskApp.Services
{
    namespace CMaaS.Task.Service
    {
        class SvcUtil
        {
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
        }
    }

}
