using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CMaaS.TaskProject.Model;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using System.Reflection.Metadata;

namespace CMaaS.TaskProject.Service
{
    class DBUtil
    {

        private static CosmosClient CosmosClient
        {
            get
            {
                if (_cosmosClient == null)
                {
                    string endpoint = Environment.GetEnvironmentVariable("endpoint");
                    string authKey = Environment.GetEnvironmentVariable("authKey");
                    _cosmosClient = new CosmosClient(endpoint, authKey);
                }

                return _cosmosClient;
            }
        }


        private static Uri _collectionLink;
        //private CosmosClient cosmosClient;
        private static CosmosClient _cosmosClient;


        private static Uri CollectionLink
        {
            get
            {
                if (_collectionLink == null)
                {
                    _collectionLink = UriFactory.CreateDocumentCollectionUri("bloomskyHealth", "Project");
                }
                return _collectionLink;
            }
        }

        public async Task<string> InsertProject(Project proj)
        {
            Container container = CosmosClient.GetContainer("bloomskyHealth", "Project");
            ItemResponse<Project> response = await container.CreateItemAsync<Project>(
            proj,
            partitionKey: new Microsoft.Azure.Cosmos.PartitionKey(proj.projectid)
            );
            
            string status = response.StatusCode.ToString();
            return status;
        }




}
}

