using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
        [FunctionName("CreateGroupTaskSet")]
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
                        GroupTask newGT = new GroupTask();
                        newGT = newGTS.grouptask[0];

                        //set new IDs for Group Task, Invidivudal Task Set (if any), and Individual Tasks (if any)
                        SvcUtil util = new SvcUtil();
                        newGT = util.SetNewIDs(newGT);

                        //add new GTS with new IDS to DB
                        DBUtil dbRepo = new DBUtil();
                        retDoc = await dbRepo.CreateGroupTaskSet(newGTS);
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

        [FunctionName("CreateGroupTask")]
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

                        //extract the new GT from the JSON 
                        //GroupTaskSet newGTS = new GroupTaskSet();
                        //newGTS = data;
                        
                        
                        GroupTask newGT = new GroupTask();
                        newGT = data;
                        //newGT = newGTS.grouptask[0];

                        //set new IDs for Group Task, Invidivudal Task Set (if any), and Individual Tasks (if any)
                        SvcUtil util = new SvcUtil();
                        newGT = util.SetNewIDs(newGT);
                        //add new GT to host GTS
                        
                        gts.grouptask.Add(newGT);
                        
                        try
                        {
                            //update the GTS in DB with new GT
                            Boolean result = await dbRepo.UpdateGTSById(gts);
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

        [FunctionName("ValidateGTSExists")]
        public static async Task<IActionResult> RunValidateGTSExists(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
        ILogger log)
        {
            try
            {
                //get send group task workflow  JSON from body
                string requestBody = new StreamReader(req.Body).ReadToEnd();
                //dynamic data = JsonConvert.DeserializeObject<List<GroupTaskSet>>(requestBody);
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
        }
}
