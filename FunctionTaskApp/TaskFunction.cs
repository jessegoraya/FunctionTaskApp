using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CMaaS.Task.Model;
using CMaaS.Task.Service;
using System.Net.Http;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs.Host.Config;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host;
using FunctionTaskApp.Services.CMaaS.Task.Service;


namespace CMaaS.Task.Function
{

    public static class FunctionTaskController
    {
        [FunctionName("CreateGTS")]
        public static async Task<IActionResult> RunCreateGroupTaskSetAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            //create the post request to create the Case Tasks

            //get send group task workflow  JSON from body
            string requestBody = new StreamReader(req.Body).ReadToEnd();
            dynamic data = JsonConvert.DeserializeObject<GroupTaskSet>(requestBody);
            
            //get input variables for query variable or header to say the tenant and the
            string tenant = req.Query["tenant"];
            tenant = tenant ?? data?.tenantid;

            string caseid = req.Query["caseid"];
            caseid = caseid ?? data.caseid;

            Document retDoc;

            if ((tenant != null) && (caseid != null))
            {
                try
                {
                    using (var client = new HttpClient())
                    {
                        //extract the new GT from the JSON 
                        GroupTaskSet newGTS = new GroupTaskSet();
                        newGTS = data;

                        SvcUtil svcUtil = new SvcUtil();
                        newGTS.taskid = svcUtil.Create(newGTS.tenantid, 0001);

                        GroupTask newGT = new GroupTask();
                        newGT = newGTS.grouptask[0];



                        SvcUtil svcIDs = new SvcUtil();
                        newGT = svcIDs.SetNewIDs(newGT);


                        DBUtil dbRepo = new DBUtil();
                        retDoc = await dbRepo.InsertGroupTaskSet(newGTS);
                        //return results
                        if (retDoc.Id != null)
                        {
                            //If the passed Case ID DOES NOT exist then create a new Group Task Set with its first Group Task
                            //create a test if tenant and customer are provided and JSON formated as GTS
                            return new OkObjectResult(retDoc);
                        }
                        else
                        {
                            //create a test if tenant and customer are provided and JSON provided is not GTS
                            return (ActionResult)new BadRequestResult();
                        }
                    }
                }
                catch (Exception)
                {
                    //create a test if something goes wrong with the request to Cosmos
                    return (ActionResult)new BadRequestResult();
                }
            }
            else
            {
                //create a test if eiter tenant and customer are not provided
                return (ActionResult)new BadRequestResult();
            }

        }


        [FunctionName("CreateGT")]
        public static async Task<IActionResult> RunCreateGroupTaskAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
        ILogger log)
        {   //create the post request to create the Case Tasks.  So send in new Case Task.

            //get send group task workflow  JSON from body


            string requestBody = new StreamReader(req.Body).ReadToEnd();
            dynamic data = JsonConvert.DeserializeObject<GroupTask>(requestBody);

            //get input variables for query variable or header to say the tenant and the
            string id = req.Query["id"];
            id = id ?? data?.id;
           
            //Document retDoc;

            if ((id != null))
            {
                try
                {
                    using (var client = new HttpClient())
                    {
                        //get the group task set where you will be inserting your new GT
                        DBUtil dbRepo = new DBUtil();                       
                        GroupTaskSet gts = dbRepo.GetGroupTaskSetByID(id);                       
                        
                        GroupTask newGT = new GroupTask();
                        newGT = data;

                        SvcUtil svcIDs = new SvcUtil();
                        newGT = svcIDs.SetNewIDs(newGT);

                        gts.grouptask.Add(newGT); 

                        
                        try
                        {

                            //update the GTS in DB with new GT
                            Boolean result = await dbRepo.UpdateGTSItem(gts);
                            if (result == true)
                            {
                                return new OkObjectResult(gts);
                            }
                        }
                        catch (Exception)
                        {
                            return (ActionResult)new BadRequestResult();
                        }

                        return (ActionResult)new BadRequestResult();

                    }
                }
                catch (Exception)



                {


                    
                    //create a test if something goes wrong with the request to Cosmos
                    return (ActionResult)new BadRequestResult();
                }
            }
            else
            {
                //create a test if eiter tenant and customer are not provided
                return (ActionResult)new BadRequestResult();
            }

        }

        [FunctionName("CreateIT")]
        public static async Task<IActionResult> RunCreateIndividualTaskSetAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
        ILogger log)
        {   //create the post request to create the Individual Tasks.  So send in new Individual Task.

            //get send individual task workflow  JSON from body
            string requestBody = new StreamReader(req.Body).ReadToEnd();
            dynamic data = JsonConvert.DeserializeObject<IndividualTask>(requestBody);
            //string itsid = req.Query["itsid"];
            string GTID = req.Query["GTID"];
            /* json format for Individual Task
				{
						"individualtaskid": "5854a419-b26b-470b-b162-382415bb6a7a", 
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
				} 
            */

            //get input variables for query variable or header to say the tenant and the


            //Document retDoc;

            if ((GTID != null))
            {
                try
                {
                    using (var client = new HttpClient())
                    {
                        IndividualTask it = new IndividualTask();
                        IndividualTaskSet its = new IndividualTaskSet
                        {
                            individualtasksetid = Guid.NewGuid(),   
                            individualtask = new List<IndividualTask>()
                        };

                        it = data;
                        SvcUtil util = new SvcUtil();
                        //update SetNewITIDs to appropriate items if assignments aren't set including status, task type, descriptiption, associated Role, assigned date and due date
                        it = util.SetNewITIDs(it);
                        its.individualtask.Add(it);

                        //create a new ITS and a Facilitating IT
                        //-Individual Task Title = “Facilitate assignments of” + Group Task Title + task
                        //-individual Task Type = Facilitating Individual Task
                        //-Individual Task Description = Group Task Description
                        //-Assigned To: Stakeholder Group Name – Facilitators
                        //-Associated Role: Stakeholder Group Name – Facilitators
                        //-Individual Task Due Date: Set to the same date as the creation date of the Group Task
                        //-Individual Task Status: Set to Open - % Complete: 0 %
                        //-Worksite / Administrative Link – Set to the home page of the Worksite/ Administrative site page. For worksite this will be the worksite under which this Individual Task is aligned
                        //-Worksite / Administrative Group Task Link – Set to the home page of the Worksite/ Administrative Group Task page where this sits under the Worksite/ Administrative

                        //get Group Task
                        try
                        {

                            DBUtil dbRepo = new DBUtil();
                            GroupTaskSet gts = dbRepo.GetGTSbyGTID(GTID);

                            //find id of Group Task which needs to be updated and then add the new ITS
                            int updGTIdx = gts.grouptask.FindIndex(item => item.grouptaskid.ToString() == GTID);
                            gts.grouptask[updGTIdx].individualtasksets.Add(its);

                            //update the GTS in DB with new GT
                            Boolean result = await dbRepo.UpdateGTSItem(gts);

                            if (result == true)
                            {
                                //setup returnoject here
                                ReturnTaskObject rTO = new ReturnTaskObject();
                                rTO.caseid = gts.caseid;
                                rTO.grouptaskid = gts.grouptask[updGTIdx].grouptaskid.ToString();
                                rTO.individualtasksetid = its.individualtasksetid.ToString();
                                return new OkObjectResult(gts);
                            }
                        }
                        catch (Exception)
                        {
                            return (ActionResult)new BadRequestResult();
                        }

                        return (ActionResult)new BadRequestResult();

                    }
                }
                catch (Exception)
                {
                    //create a test if something goes wrong with the request to Cosmos
                    return (ActionResult)new BadRequestResult();
                }
            }
            else
            {
                //create a test if eiter tenant and customer are not provided
                return (ActionResult)new BadRequestResult();
            }

        }

        [FunctionName("UpdateIT")]
        public static async Task<IActionResult> RunUpdateIndividualTaskSetAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
        ILogger log)
        {   //create the post request to create the Individual Tasks.  So send in new Individual Task.
            try
            {
                //get send individual task workflow  JSON from body
                string requestBody = new StreamReader(req.Body).ReadToEnd();
                dynamic data = JsonConvert.DeserializeObject<IndividualTask>(requestBody);
                Guid itid; 

                itid = data?.individualtaskid;

                //get document id using itid
                DBUtil dbUtil = new DBUtil();
                ReturnTaskObject rto = dbUtil.GetRTOIDbyITID(itid.ToString());


                if (rto.id != "")
                {
                    //send update path for document along with updated individual task set index and indvididual task index
                    string filter = "FROM Task t JOIN g in t.GroupTask JOIN its IN g.IndividualTaskSets JOIN it IN its.IndividualTask WHERE it.IndividualTaskID ='" + itid + "'";
                    string status = await dbUtil.UpdateIT(rto, data, filter);
                    if (status == "200") 
                    {
                        return new OkObjectResult(status);
                    }
                    else
                    {
                        return new BadRequestResult();
                    }
                }

                else
                {
                    return (ActionResult)new BadRequestResult();

                }
            }
            catch (Exception e)
            {
                //create a test if something goes wrong with the request to Cosmos
                log.LogError(e.Message + ".  Inner Exception: " + e.InnerException);
                return (ActionResult)new BadRequestResult();
            }

        }

        [FunctionName("ValidateGTS")]
        public static async Task<IActionResult> RunValidateGTSExists(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
        ILogger log)
        {
            try
            {
                //get send group task workflow  JSON from body
                string requestBody = new StreamReader(req.Body).ReadToEnd();
                dynamic data = JsonConvert.DeserializeObject(requestBody);

                //get input variables for query variable or header to say the tenant and the
                string tenant = req.Query["tenant"];
                tenant = tenant ?? data?.tenantid;

                string caseid = req.Query["caseid"];
                caseid = caseid ?? data.caseid;

 
                if ((tenant != null) && (caseid != null))
                {
                        using (var client = new HttpClient())
                        {

                            DBUtil dbRepo = new DBUtil();
                            List<GroupTaskSet> gtsList = await dbRepo.GetGroupTaskSetByTenantAndCase(caseid, tenant);
                            string gtsExists;

                            //return results
                            if (gtsList.Count > 0)
                            {
                                int gtItem = gtsList.Count - 1;
                                gtsExists = gtsList[0].id;
                                return new OkObjectResult(gtsExists);
                            }
                            else
                            {
                                //If the passed Case ID DOES NOT exist then create a new Group Task Set with its first Group Task
                                //create a test if tenant and customer are provided and JSON formated as GTS
                                gtsExists = "None";
                                return new OkObjectResult(gtsExists);
                            }
                        }
                }
                else
                {
                    //create a test if eiter tenant and customer are not provided
                    log.LogInformation("Not caught as an exception." + requestBody.ToString());
                    return (ActionResult)new BadRequestResult();
                }

                }
            catch (Exception e)
            {
                //create a test if something goes wrong with the request to Cosmos
                log.LogError(e.Message + ".  Inner Exception: " + e.InnerException);
                return (ActionResult)new BadRequestResult();
            }
        }

        [FunctionName("UpdateSingleGTProp")]
        //Use patch operation to update a single property on the GT without having to feed the entire GT
        public static async Task<IActionResult> RunUpdateSingleGTPropSetAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
        ILogger log)
        {   //create the post request to create the Individual Tasks.  So send in new Individual Task.
            try
            {
                //get  propname,  propvalue,  cosmosid,  taskid from dynamic JSON
                string requestBody = new StreamReader(req.Body).ReadToEnd();
                JObject data = JsonConvert.DeserializeObject<JObject>(requestBody);
                string propname = (string)data.SelectToken("$.propname");
                string propvalue = (string)data.SelectToken("$.propvalue");
                string cosmosid = (string)data.SelectToken("$.cosmosid");
                string taskid = (string)data.SelectToken("$.taskid");

                DBUtil dbUtil = new DBUtil();
                String status = await dbUtil.UpdateGTProp(propname, propvalue, cosmosid, taskid);

                if (status == "OK")
                {
                    return new OkObjectResult(status);
                }
                else
                {
                    return new BadRequestResult();
                }
            }
            catch (Exception e)
            {
                //create a test if something goes wrong with the request to Cosmos
                log.LogError(e.Message + ".  Inner Exception: " + e.InnerException);
                return (ActionResult)new BadRequestResult();
            }
        }

        [FunctionName("ValidateCompletedIT")]
        //function allows the parent GTID (and other values in the return task object) by sending the individual task id
        public static IActionResult RunValidateCompletedIT(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
        ILogger log)
        {
            try
            {
                //get IT JSON from body
                string requestBody = new StreamReader(req.Body).ReadToEnd();
                IndividualTask compIT = JsonConvert.DeserializeObject<IndividualTask>(requestBody);
                ReturnTaskObject rto = new ReturnTaskObject();
                rto.valid = false;


                if ((compIT.individualtaskcompleteddate == DateTime.MinValue) && (compIT.previouslysent != true))
                {
                    //if the task is being completed for the first time then mark the completed date and return a true validation
                    compIT.individualtaskcompleteddate = DateTime.Today;

                    //get the RTO (Return Task Object) by passing the ITID in order to get the GTID.  If you have a value then its true
                    DBUtil dbUtil = new DBUtil();
                    rto = dbUtil.GetRTOIDbyITID(compIT.individualtaskid.ToString());
                    rto.valid = true;
                    return new OkObjectResult(rto);
                }
                else
                {
                    return new OkObjectResult(rto);
                }

            }
            catch (Exception e)
            {
                //create a test if something goes wrong with the request to Cosmos
                log.LogError(e.Message + ".  Inner Exception: " + e.InnerException);
                return (ActionResult)new BadRequestResult();
            }
        }
    }

}

