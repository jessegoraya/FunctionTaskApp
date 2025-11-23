using System;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using System.Linq;
using System.Collections.Generic;
using CMaaS.TaskProject.Model;

namespace CMaaS.TaskProject.DAL
{
    public class DBUtil
    {

        private readonly CosmosClient cosmosClient;
        private readonly Container container;

        private const string DatabaseName = "bloomskyHealth";
        private const string ContainerName = "Project";

        public DBUtil()
        {
            string cosmosConnectionString = Environment.GetEnvironmentVariable("CosmosDBConnection");
            cosmosClient = new CosmosClient(cosmosConnectionString);
            container = cosmosClient.GetContainer(DatabaseName, ContainerName);
        }


        public async Task<bool> InsertProject(Project item)
        {
            //item.Id ??= Guid.NewGuid().ToString();
            ItemResponse<Project> response = await container.CreateItemAsync(item, new PartitionKey(item.tenantid));
            return response.StatusCode == System.Net.HttpStatusCode.OK;
        }


        public async Task<Dictionary<string, Project>> GetProjectDatabyProjectIDList(List<string> projectIds, string tenantid)
        {
           //return project data based on a list of project ids being input
            var projectLookup = new Dictionary<string, Project>();
            foreach (var pid in projectIds)
            {
                try
                {
                    ItemResponse<Project> projectResponse = await container.ReadItemAsync<Project>(
                        pid,
                        new PartitionKey(tenantid)
                    );
                    projectLookup[pid] = projectResponse.Resource;
                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Project not found, skip
                }

            }
            return projectLookup;

        }

        public async Task<List<string>> GetProjectIdsForManagerAsync(string userEmail, string tenantid)
        {
            var query = new QueryDefinition(
                "SELECT p.id AS ProjectID FROM p JOIN m IN p.AssociatedManagers WHERE m.personEmail = @email and p.tenantID = @tenantID"
            )
                .WithParameter("@email", userEmail)
                .WithParameter("@tenantID", tenantid);

            var results = new List<string>();
            var requestOptions = new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(tenantid) 
            };

            using (var iterator = container.GetItemQueryIterator<dynamic>(query, requestOptions: requestOptions))
            {
                while (iterator.HasMoreResults)
                {
                    foreach (var item in await iterator.ReadNextAsync())
                    {
                        results.Add(item.ProjectID.ToString());
                    }
                }
            }
            return results;
        }
    }
}



