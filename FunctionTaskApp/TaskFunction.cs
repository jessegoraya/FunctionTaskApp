using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Taslow.Task.Model;
using Taslow.Task.Service;
using Taslow.Shared.Model;
using System.Collections.Generic;
using System.Linq;
using Taslow.Task.DAL.Interface;
using Taslow.Task.Client.Interface;

namespace Taslow.Task.Function
{

    public class FunctionTaskController
    {
        private readonly ITaskDBUtil _taskDb;
        private readonly IProjectServiceClient _projSvcClient;

        public FunctionTaskController(ITaskDBUtil taskDb, IProjectServiceClient projSvcClient)
        {
            _taskDb = taskDb;
            _projSvcClient = projSvcClient;
        }

        [FunctionName("Ping")]
        public IActionResult Ping(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ping")]
              HttpRequest req)
        {
            return new OkObjectResult("pong");
        }

        [FunctionName("AddGroupTaskSet")]
        public async Task<IActionResult> RunAddGroupTaskSetAsync(
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

            GroupTaskSet result = await _taskDb.InsertGroupTaskSet(newGTS);

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
        public async Task<IActionResult> RunGetGroupTaskSetByIdAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "grouptaskset/{id}/{tenantid}")] HttpRequest req,
            string id,
            string tenantid,
            ILogger log)
        {
            log.LogInformation($"GetGroupTaskSetById function processed a request for id: {id}, tenantid: {tenantid}");

            GroupTaskSet result = await _taskDb.GetGroupTaskSet(id, tenantid);

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
        public async Task<IActionResult> RunGetGroupTaskSetByProjectIdAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "grouptasksetbyproject/{projectid}/{tenantid}")] HttpRequest req,
            string projectid,
            string tenantid,
            ILogger log)
        {
            log.LogInformation($"GetGroupTaskSetByCaseId function processed a request for id: {projectid}, tenantid: {tenantid}");

            GroupTaskSet result = await _taskDb.GetGroupTaskSetByProjectId(projectid, tenantid);

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
        public async Task<IActionResult> RunUpdateGroupTaskSetAsync(
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

            bool result = await _taskDb.UpdateGroupTaskSet(id, tenantid, updatedGTS);

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
        public async Task<IActionResult> RunDeleteGroupTaskSetAsync(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "grouptaskset/{id}/{tenantid}")] HttpRequest req,
            string id,
            string tenantid,
            ILogger log)
        {
            log.LogInformation($"DeleteGroupTaskSet function processed a request for id: {id}, tenantid: {tenantid}");

            bool deleted = await _taskDb.DeleteGroupTaskSet(id, tenantid);

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
        public async Task<IActionResult> AddGroupTaskToGTS(
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

                bool success = await _taskDb.CreateGroupTaskAsync(id, tenantid, NewGT);
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
        public async Task<IActionResult> UpdateGroupTaskinGTS(
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

                bool success = await _taskDb.UpdateGroupTaskAsync(id, tenantid, updGT);
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
        public async Task<IActionResult> AddIndividualTaskToGT(
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

                bool success = await _taskDb.CreateIndividualTaskAsync(id, tenantid, gtid, NewIT);

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
        public async Task<IActionResult> UpdateIndividualTaskinGT(
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

                bool success = await _taskDb.UpdateIndividualTaskAsync(id, tenantid, gtid, updIT);
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
        public async Task<IActionResult> RunGetGroupTaskSetByTenantAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "taskcontextdto/{tenantid}/{person}")] HttpRequest req,
            string tenantid,
            string person,
            ILogger log)
        {
            log.LogInformation($"GetGTSDTOyTenantandPerson function processed a request for tenantid: {tenantid}");

            List<TaskContextDTO> result = await _taskDb.GetGTContextDTO(tenantid, person);

            if (result != null)
            {
                return new OkObjectResult(result);
            }
            else
            {
                return new NotFoundResult();
            }
        }

        //Provide a manager name so that you can return on all tasks across projects where that user is a manager
        [FunctionName("GetTasksForManagedProjects")]
        public async Task<IActionResult> GetTasksForManagedProjects(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "getmgrtaskcontextdto/{tenantid}/{manager}")] HttpRequest req,
        string tenantid,
        string manager,
        ILogger log)
        {
            if (string.IsNullOrEmpty(manager))
                return new BadRequestObjectResult("Manager email is required.");

            // Step 1: Get all project IDs where user is a manager
            var projectIds = await _projSvcClient.GetProjectIdsForManagerAsync(tenantid, manager);

            if (projectIds == null || !projectIds.Any())
                return new OkObjectResult(new List<TaskContextDTO>()); // no results

            // Step 2: Fetch all tasks for those project IDs
            var tasks = await _taskDb.GetTasksByProjectIdsAsync(tenantid, projectIds);

            return new OkObjectResult(tasks);
        }

    }

}
