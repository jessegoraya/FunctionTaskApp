using System;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using System.Linq;
using System.Collections.Generic;
using Taslow.Project.Model;
using Taslow.Shared.Model;
using Taslow.Project.DAL.Interface;
using Microsoft.Extensions.Configuration;

namespace Taslow.Project.DAL
{
    public class DBUtil : IProjectDBUtil
    {
        private readonly IConfiguration _configuration;
        private CosmosClient cosmosClient;
        private static Container container;

        private const string DatabaseName = "bloomskyHealth";
        private const string ContainerName = "Project";

        public DBUtil(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private Container Container
        {
            get
            {
                if (container != null)
                    return container;

                var connectionString = _configuration["CosmosDBConnection"];

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new InvalidOperationException(
                        "CosmosDBConnection setting is missing");
                }

                cosmosClient = new CosmosClient(connectionString);
                container = cosmosClient.GetContainer(DatabaseName, ContainerName);

                return container;
            }
        }


        private ProjectPersonDTO MapToDTO(AssociatedPeople person,string role)
        {
            return new ProjectPersonDTO
            {
                AssociatedPersonId = person.associatedpersonid,
                PersonName = person.personname,
                PersonAliases = person.personaliases,
                PersonEmail = person.personemail,
                Role = person.role
            };
        }

        public async Task<bool> InsertProject(TaskProject item)
        {
            //item.Id ??= Guid.NewGuid().ToString();
            ItemResponse<TaskProject> response = await container.CreateItemAsync(item, new PartitionKey(item.tenantid));
            return response.StatusCode == System.Net.HttpStatusCode.OK;
        }


        public async Task<Dictionary<string, TaskProject>> GetProjectDatabyProjectIDList(List<string> projectIds, string tenantid)
        {
           //return project data based on a list of project ids being input
            var projectLookup = new Dictionary<string, TaskProject>();
            foreach (var pid in projectIds)
            {
                try
                {
                    ItemResponse<TaskProject> projectResponse = await container.ReadItemAsync<TaskProject>(
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

        public async Task<List<TaskProject>> GetActiveProjectsByTenantAsync(string tenantId)
        {
            var query = new QueryDefinition(@"
            SELECT 
                c.id,
                c.ProjectID,
                c.ProjectName
            FROM c
            WHERE c.TenantID = @tenantId
              AND LOWER(c.ProjectStatus) = 'active'
            ORDER BY c.ProjectName
        ")
            .WithParameter("@tenantId", tenantId);

            var results = new List<TaskProject>();

            using FeedIterator<TaskProject> iterator =
                container.GetItemQueryIterator<TaskProject>(
                    query,
                    requestOptions: new QueryRequestOptions
                    {
                        PartitionKey = new PartitionKey(tenantId)
                    });

            while (iterator.HasMoreResults)
            {
                FeedResponse<TaskProject> response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }

        public  async Task<object> GetProjectAssociationsAsync(
        string tenantId,
        string projectId,
        string mode,
        string role)
        {
            var response = await container.ReadItemAsync<TaskProject>(
            id: projectId,
            partitionKey: new PartitionKey(tenantId));

            var project = response.Resource;

            var people = (project.associatedpeople ?? new List<AssociatedPeople>())
                .Select(p => MapToDTO(p, "Person"));

            var managers = (project.associatedmanagers ?? new List<AssociatedPeople>())
                .Select(m => MapToDTO(m, "Manager"));

            // ROLE FILTER
            if (role == "people")
                return new { role, people = people.ToList() };

            if (role == "managers")
                return new { role, people = managers.ToList() };

            // MODE SWITCH
            if (mode == "merged")
            {
                return new
                {
                    mode,
                    people = people.Concat(managers)
                                    .GroupBy(p => p.AssociatedPersonId)
                                    .Select(g => g.First())
                                    .ToList()
                };
            }

            // DEFAULT: SEPARATE
            return new
            {
                mode = "separate",
                associatedPeople = people.ToList(),
                associatedManagers = managers.ToList()
            };
        }

        public async Task<Dictionary<string, ProjectDTO>> GetProjectsByIdListAsync(List<string> projectIds, string tenantId)
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE ARRAY_CONTAINS(@ids, c.projectid)")
                .WithParameter("@ids", projectIds);

            var results = new Dictionary<string, ProjectDTO>();

            using var iterator = container.GetItemQueryIterator<ProjectDTO>(
                query,
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(tenantId)
                });

            while (iterator.HasMoreResults)
            {
                foreach (var project in await iterator.ReadNextAsync())
                {
                    results[project.Id] = project;
                }
            }

            return results;

        }


    }
}



