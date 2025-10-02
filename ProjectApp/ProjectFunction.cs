using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using CMaaS.TaskProject.Model;
using CMaaS.TaskProject.Service;
using CMaaS.TaskProject.DAL;
using Microsoft.Azure.Documents;

namespace CMaaS.TaskProject.Function
{
    public static class ProjectTaskController
    {
        [FunctionName("CreateProject")]
        public static async Task<IActionResult> RunCreateProjectAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            //create the post request to create the Case Tasks

            //get send group task workflow  JSON from body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject<Project>(requestBody);

            string tenant = req.Query["tenant"];
            tenant = tenant ?? data?.tenantid;



            if (tenant != null) 
            {
                try
                {
                    Project newProj = new Project();
                    newProj = data;

                    SvcUtil svcUtil = new SvcUtil();
                    newProj.tenantid = svcUtil.Create(newProj.tenantid);
                    Guid g = Guid.NewGuid();
                    newProj.Id = g.ToString();

                    DBUtil dbRepo = new DBUtil();
                    bool responseMessage = await dbRepo.InsertProject(newProj);

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
    }
}
