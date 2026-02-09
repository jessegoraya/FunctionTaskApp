using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using Taslow.Project.Model;
using Taslow.Project.Service;
using Taslow.Project.DAL.Interface;
using Taslow.Shared.Model;
using System.Linq;

namespace Taslow.Project.Function
{
    public class ProjectTaskController
    {
        private readonly IProjectDBUtil _projectDb;

        public ProjectTaskController(IProjectDBUtil projectDb)
        {
            _projectDb = projectDb;
        }

        [FunctionName("CreateProject")]
        public async Task<IActionResult> RunCreateProjectAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");


            //get create project  JSON from body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject<TaskProject>(requestBody);

            string tenant = req.Query["tenant"];
            tenant = tenant ?? data?.tenantid;



            if (tenant != null) 
            {
                try
                {
                    TaskProject newProj = new TaskProject();
                    newProj = data;

                    SvcUtil svcUtil = new SvcUtil();
                    newProj.tenantid = svcUtil.Create(newProj.tenantid);
                    Guid g = Guid.NewGuid();
                    newProj.Id = g.ToString();

                    bool responseMessage = await _projectDb.InsertProject(newProj);

                    return new OkObjectResult(responseMessage);
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


        [FunctionName("GetActiveProjectsByTenant")]
        public async Task<IActionResult> GetActiveProjectsByTenant(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "projects/active/{tenantId}")]
        HttpRequest req,
            string tenantId,
            ILogger log)
        {
            log.LogInformation($"GetActiveProjectsByTenant started. TenantId={tenantId}");

            if (string.IsNullOrWhiteSpace(tenantId))
            {
                return new BadRequestObjectResult("TenantId is required.");
            }

            try
            {
                var projects = await _projectDb.GetActiveProjectsByTenantAsync(tenantId);
                return new OkObjectResult(projects);
            }
            catch (Exception ex)
            {
                // Anything unexpected
                log.LogError(
                    ex,
                    "Unhandled exception in GetActiveProjectsByTenant. TenantId={TenantId}",
                    tenantId
                );

                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        [FunctionName("GetProjectAssociations")]
        public  async Task<IActionResult> GetProjectAssociations(
        [HttpTrigger(
            AuthorizationLevel.Function,
            "get",
            Route = "projects/{tenantId}/{projectId}/associations")]
        HttpRequest req,
        string tenantId,
        string projectId,
        ILogger log)
        {
            try
            {
                var mode = req.Query["mode"].ToString()?.ToLower() ?? "separate";
                var role = req.Query["role"].ToString()?.ToLower() ?? "all";

                var result = await _projectDb.GetProjectAssociationsAsync(
                    tenantId,
                    projectId,
                    mode,
                    role);

                return new OkObjectResult(result);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error retrieving project associations");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        [FunctionName("GetProjectsBatch")]
        public async Task<IActionResult> GetProjectsBatch(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "projects/batch")] HttpRequest req,
        ILogger log)
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonConvert.DeserializeObject<ProjectBatchRequest>(body);

            if (request == null ||
                string.IsNullOrWhiteSpace(request.TenantId) ||
                request.ProjectIds == null ||
                !request.ProjectIds.Any())
            {
                return new BadRequestObjectResult("Invalid request payload.");
            }


            var projects = await _projectDb.GetProjectsByIdListAsync(
                request.ProjectIds,
                request.TenantId);

            var response = new ProjectBatchResponse
            {
                Projects = projects
            };

            return new OkObjectResult(response);
        }

        [FunctionName("GetProjectIdsForManager")]
            public async Task<IActionResult> GetProjectIdsForManager(
        [HttpTrigger(AuthorizationLevel.Function, "get",
            Route = "projects/managed/{tenantId}/{manager}")]
        HttpRequest req,
        string tenantId,
        string manager)
        {
            var projectIds = await _projectDb.GetProjectIdsForManagerAsync(manager, tenantId);
            return new OkObjectResult(projectIds);
        }

    }
}

