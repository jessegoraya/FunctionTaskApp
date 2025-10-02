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
using CMaaS.Task.DAL;
using CMaaS.Task.Service;
using System.Collections.Generic;

namespace CMaaS.Task.Function
{
    public static class FunctionTaskController
    {
        [FunctionName("AddGroupTaskSet")]
        public static async Task<IActionResult> RunAddGroupTaskSetAsync(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "grouptaskset")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("AddGroupTaskSet function processed a request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            GroupTaskSet newGTS = JsonConvert.DeserializeObject<GroupTaskSet>(requestBody);

            if (newGTS == null)
            {
                return new BadRequestObjectResult("Invalid payload");
            }

            newGTS.id = Guid.NewGuid().ToString();
            DBUtil dbRepo = new DBUtil();
            GroupTaskSet result = await dbRepo.InsertGroupTaskSet(newGTS);

            if (result != null && !string.IsNullOrEmpty(result.id))
            {
                return new OkObjectResult(result);
            }
            else
            {
                return new BadRequestObjectResult("Could not add GroupTaskSet");
            }
        }

        //use this function to get a Group Task when you have the GTS Id (i.e. ID) and Tenant ID
        [FunctionName("GetGroupTaskSetById")]
        public static async Task<IActionResult> RunGetGroupTaskSetByIdAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "grouptaskset/{id}/{tenantid}")] HttpRequest req,
            string id,
            string tenantid,
            ILogger log)
        {
            log.LogInformation($"GetGroupTaskSetById function processed a request for id: {id}, tenantid: {tenantid}");

            DBUtil dbRepo = new DBUtil();
            GroupTaskSet result = await dbRepo.GetGroupTaskSet(id, tenantid);

            if (result != null)
            {
                return new OkObjectResult(result);
            }
            else
            {
                return new NotFoundResult();
            }
        }

        //use this function to get a Group Task when you have the Case Id (i.e. Project) and Tenant ID
        [FunctionName("GetGroupTaskSetByProjectId")]
        public static async Task<IActionResult> RunGetGroupTaskSetByProjectIdAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "grouptasksetbyproject/{projectid}/{tenantid}")] HttpRequest req,
            string projectid,
            string tenantid,
            ILogger log)
        {
            log.LogInformation($"GetGroupTaskSetByCaseId function processed a request for id: {projectid}, tenantid: {tenantid}");

            DBUtil dbRepo = new DBUtil();
            GroupTaskSet result = await dbRepo.GetGroupTaskSetByProjectId(projectid, tenantid);

            if (result != null)
            {
                return new OkObjectResult(result);
            }
            else
            {
                return new NotFoundResult();
            }
        }

        [FunctionName("UpdateGroupTaskSet")]
        public static async Task<IActionResult> RunUpdateGroupTaskSetAsync(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "grouptaskset/{id}/{tenantid}")] HttpRequest req,
            string id,
            string tenantid,
            ILogger log)
        {
            log.LogInformation($"UpdateGroupTaskSet function processed a request for id: {id}, tenantid: {tenantid}");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            GroupTaskSet updatedGTS = JsonConvert.DeserializeObject<GroupTaskSet>(requestBody);

            if (updatedGTS == null)
            {
                return new BadRequestObjectResult("Invalid payload");
            }

            DBUtil dbRepo = new DBUtil();
            bool result = await dbRepo.UpdateGroupTaskSet(id, tenantid, updatedGTS);

            if (result != true)
            {
                return new OkObjectResult(result);
            }
            else
            {
                return new NotFoundResult();
            }
        }

        [FunctionName("DeleteGroupTaskSet")]
        public static async Task<IActionResult> RunDeleteGroupTaskSetAsync(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "grouptaskset/{id}/{tenantid}")] HttpRequest req,
            string id,
            string tenantid,
            ILogger log)
        {
            log.LogInformation($"DeleteGroupTaskSet function processed a request for id: {id}, tenantid: {tenantid}");

            DBUtil dbRepo = new DBUtil();
            bool deleted = await dbRepo.DeleteGroupTaskSet(id, tenantid);

            if (deleted)
            {
                return new OkResult();
            }
            else
            {
                return new NotFoundResult();
            }
        }


       [FunctionName("AddGroupTaskToGTS")]
       public static async Task<IActionResult> AddGroupTaskToGTS(
       [HttpTrigger(AuthorizationLevel.Function, "post", Route = "addgrouptasktogts/{id}/{tenantid}/")] HttpRequest req,
       string id,
       string tenantid,
       ILogger log)
        {
            log.LogInformation("Processing request to add a new GroupTask.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            GroupTask NewGT;

            try
            {
                NewGT = JsonConvert.DeserializeObject<GroupTask>(requestBody);
                if (NewGT == null)
                {
                    return new BadRequestObjectResult("Invalid GroupTask payload.");
                }

                if (string.IsNullOrEmpty(tenantid))
                {
                    return new BadRequestObjectResult("Missing required query parameter: tenantid");
                }
                SvcUtil svc = new SvcUtil();
                NewGT = svc.SetNewIDs(NewGT);

                DBUtil dbRepo = new DBUtil();
                bool success = await dbRepo.CreateGroupTaskAsync(id, tenantid, NewGT);
                if (success)
                {
                    return new OkObjectResult($"GroupTask added to GroupTaskSet {id}.");
                }
                else
                {
                    return new StatusCodeResult(StatusCodes.Status500InternalServerError);
                }
            }
            catch (JsonException ex)
            {
                log.LogError(ex, "Failed to deserialize GroupTask.");
                return new BadRequestObjectResult("Malformed JSON.");
            }

        }

        [FunctionName("UpdateGroupTaskinGTS")]
        public static async Task<IActionResult> UpdateGroupTaskinGTS(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "updgrouptask/{id}/{tenantid}/")] HttpRequest req, string id, string tenantid,
       ILogger log)
        {
            try
            {
                // Read and deserialize body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                GroupTask updGT = JsonConvert.DeserializeObject<GroupTask>(requestBody);

                if (updGT == null)
                {
                    return new BadRequestObjectResult("Error converting json");

                }

                DBUtil dbRepo = new DBUtil();
                bool success = await dbRepo.UpdateGroupTaskAsync(id, tenantid, updGT);
                if (success)
                {
                    return new OkObjectResult($"GroupTask added to GroupTaskSet {id}.");
                }
                else
                {
                    return new StatusCodeResult(StatusCodes.Status500InternalServerError);
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to deserialize GroupTask.");
                return new BadRequestObjectResult("Malformed JSON.");
            }
        }

        [FunctionName("AddIndividualTaskToGT")]
        public static async Task<IActionResult> AddIndividualTaskToGT(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "addindtask/{id}/{tenantid}/{gtid}/")] HttpRequest req,
        string id,
        string tenantid,
        string gtid,
        ILogger log)
        {
            log.LogInformation("Processing request to add a new IndividualTask.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            IndividualTask NewIT;

            try
            {
                NewIT = JsonConvert.DeserializeObject<IndividualTask>(requestBody);
                if (NewIT == null)
                {
                    return new BadRequestObjectResult("Invalid IndividualTask payload.");
                }

                if (string.IsNullOrEmpty(tenantid))
                {
                    return new BadRequestObjectResult("Missing required query parameter: tenantid");
                }
                SvcUtil svc = new SvcUtil();
                NewIT = svc.SetNewITIDs(NewIT);

                DBUtil dbRepo = new DBUtil();
                bool success = await dbRepo.CreateIndividualTaskAsync(id, tenantid, gtid, NewIT);

                if (success)
                {
                    return new OkObjectResult($"IndividualTask added to GroupTask {gtid}.");
                }
                else
                {
                    return new StatusCodeResult(StatusCodes.Status500InternalServerError);
                }
            }
            catch (JsonException ex)
            {
                log.LogError(ex, "Failed to deserialize GroupTask.");
                return new BadRequestObjectResult("Malformed JSON.");
            }

        }

        [FunctionName("UpdateIndividualTaskinGT")]
        public static async Task<IActionResult> UpdateIndividualTaskinGT(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "updindtask/{id}/{tenantid}/{gtid}/")] HttpRequest req, string id, string gtid, string tenantid,
        ILogger log)
        {
            try
            {
                // Read and deserialize body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                IndividualTask updIT = JsonConvert.DeserializeObject<IndividualTask>(requestBody);

                if (updIT == null)
                {
                    return new BadRequestObjectResult("Error converting json");

                }

                DBUtil dbRepo = new DBUtil();
                bool success = await dbRepo.UpdateIndividualTaskAsync(id, tenantid, gtid, updIT);
                if (success)
                {
                    return new OkObjectResult($"IndividualTask added to GroupTask {gtid} with document {id}.");
                }
                else
                {
                    return new StatusCodeResult(StatusCodes.Status500InternalServerError);
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to deserialize GroupTask.");
                return new BadRequestObjectResult("Malformed JSON.");
            }
        }

        //Get GTS Context DTO objects by Tenant and Person 
        [FunctionName("GetGTContextDTObyTenantandPerson")]
        public static async Task<IActionResult> RunGetGroupTaskSetByTenantAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "taskcontextdto/{tenantid}/{person}")] HttpRequest req,
            string tenantid,
            string person,
            ILogger log)
        {
            log.LogInformation($"GetGTSDTOyTenantandPerson function processed a request for tenantid: {tenantid}");

            DBUtil dbRepo = new DBUtil();
            List<TaskContextDTO> result = await dbRepo.GetGTContextDTO(tenantid, person);

            if (result != null)
            {
                return new OkObjectResult(result);
            }
            else
            {
                return new NotFoundResult();
            }
        }

    }

}
