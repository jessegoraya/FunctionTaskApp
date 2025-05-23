﻿using System;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using CMaaS.Task.Model;
using System.Linq;
using System.Collections.Generic;

namespace CMaaS.Task.DAL
{
    public class DBUtil
    {
        private readonly CosmosClient cosmosClient;
        private readonly Container container;

        private const string DatabaseName = "bloomskyHealth";
        private const string ContainerName = "GroupTaskSet";

        public DBUtil()
        {
            string cosmosConnectionString = Environment.GetEnvironmentVariable("CosmosDBConnection");
            cosmosClient = new CosmosClient(cosmosConnectionString);
            container = cosmosClient.GetContainer(DatabaseName, ContainerName);
        }

        public async Task<GroupTaskSet> InsertGroupTaskSet(GroupTaskSet item)
        {
            item.id ??= Guid.NewGuid().ToString();
            //item.PartitionKey ??= item.tenantid;  // If you use a partition key other than tenantid, update this

            ItemResponse<GroupTaskSet> response = await container.CreateItemAsync(item, new PartitionKey(item.tenantid));
            return response.Resource;
        }

        //replaces original GetGroupTaskSetByTenantandCase (string caseid, string tenantid)
        //this should also removed the need for QueryGroupTaskSets<GroupTaskSet>(IQueryable<GroupTakSet> query
        public async Task<GroupTaskSet> GetGroupTaskSet(string id, string tenantid)
        {
            try
            {
                ItemResponse<GroupTaskSet> response = await container.ReadItemAsync<GroupTaskSet>(id, new PartitionKey(tenantid));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<GroupTaskSet> GetGroupTaskSetByProjectId(string projectid, string tenantid)
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.ProjectID = @projectid AND c.TenantID = @tenantid")
                .WithParameter("@projectid", projectid)
                .WithParameter("@tenantid", tenantid);

            using (FeedIterator<GroupTaskSet> resultSet = container.GetItemQueryIterator<GroupTaskSet>(
                query,
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(tenantid)
                }))
            {
                while (resultSet.HasMoreResults)
                {
                    FeedResponse<GroupTaskSet> response = await resultSet.ReadNextAsync();
                    GroupTaskSet item = response.FirstOrDefault();
                    if (item != null)
                    {
                        return item;
                    }
                }
            }

            return null; // or throw an exception if you want to require a match
        }


        //replaces original UpdateGTSbyId(GroupTaskSet updatedGTS)
        public async Task<bool> UpdateGroupTaskSet(string id, string tenantid, GroupTaskSet updatedItem)
        {
            try
            {
                updatedItem.id = id;
                updatedItem.tenantid = tenantid;

                ItemResponse<GroupTaskSet> response = await container.ReplaceItemAsync(updatedItem, id, new PartitionKey(tenantid));
                return true;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        public async Task<bool> DeleteGroupTaskSet(string id, string tenantid)
        {
            try
            {
                await container.DeleteItemAsync<GroupTaskSet>(id, new PartitionKey(tenantid));
                return true;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        public async Task<bool> CreateGroupTaskAsync(string id, string TenantID, GroupTask newGroupTask)
        {
            try
            {
                var patchOperations = new List<PatchOperation>
                {
                    PatchOperation.Add("/GroupTask/-", newGroupTask) // Append to the GroupTask array
                };

                ItemResponse<GroupTaskSet> response = await container.PatchItemAsync<GroupTaskSet>(
                    id: id,
                    partitionKey: new PartitionKey(TenantID),
                    patchOperations: patchOperations
                );

                return response.StatusCode == System.Net.HttpStatusCode.OK;
            }
            catch (CosmosException ex)
            {
                Console.WriteLine($"Error adding GroupTask: {ex.StatusCode} - {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateGroupTaskAsync(string id, string tenantid, GroupTask updGT)
        {
            try
            {
                // Step 1: Read the document
                var response = await container.ReadItemAsync<GroupTaskSet>(
                    id: id,
                    partitionKey: new PartitionKey(tenantid)
                );
                var groupTaskSet = response.Resource;

                // Step 2: Find the index of the GroupTask to replace
                int index = groupTaskSet.grouptask.FindIndex(gt => gt.grouptaskid == updGT.grouptaskid);
                if (index == -1)
                {
                    throw new InvalidOperationException("GroupTask not found in document.");
                }

                // Step 3: Perform patch to replace the GroupTask at the correct index
                var patchOps = new List<PatchOperation>
                {
                    PatchOperation.Replace($"/GroupTask/{index}", updGT)
                };

                await container.PatchItemAsync<GroupTaskSet>(
                    id: id,
                    partitionKey: new PartitionKey(tenantid),
                    patchOperations: patchOps
                );

                return response.StatusCode == System.Net.HttpStatusCode.OK;
            }
            catch (CosmosException ex)
            {
                Console.WriteLine($"Error adding GroupTask: {ex.StatusCode} - {ex.Message}");
                return false;
            }

        }

        public async Task<bool> CreateIndividualTaskAsync(string id, string tenantid, string gtid,  IndividualTask newIndividualTask)
        {
            try
            {
                // Step 1: Read the document
                var response = await container.ReadItemAsync<GroupTaskSet>(
                    id: id,
                    partitionKey: new PartitionKey(tenantid)
                );
                var groupTaskSet = response.Resource;

                int indGT = groupTaskSet.grouptask.FindIndex(gt => gt.grouptaskid == gtid);
                var patchOperations = new List<PatchOperation>
                {
                    PatchOperation.Add($"/GroupTask/{indGT}/IndividualTaskSets/0/IndividualTask/-", newIndividualTask) // Append to the GroupTask array
                };

                ItemResponse<GroupTaskSet> addresponse = await container.PatchItemAsync<GroupTaskSet>(
                    id: id,
                    partitionKey: new PartitionKey(tenantid),
                    patchOperations: patchOperations
                );

                return addresponse.StatusCode == System.Net.HttpStatusCode.OK;
            }
            catch (CosmosException ex)
            {
                Console.WriteLine($"Error adding GroupTask: {ex.StatusCode} - {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateIndividualTaskAsync(string id, string tenantid, string grouptaskid, IndividualTask updIT)
        {
            try
            {
                // Step 1: Read the document
                var response = await container.ReadItemAsync<GroupTaskSet>(
                    id: id,
                    partitionKey: new PartitionKey(tenantid)
                );
                var groupTaskSet = response.Resource;


                // Step 2: Find the index of the IndividualTask to replace by first getting the GT index
                int indGT = groupTaskSet.grouptask.FindIndex(gt => gt.grouptaskid == grouptaskid);
                int indIT = groupTaskSet.grouptask[indGT].individualtasksets[0].individualtask.FindIndex(it => it.individualtaskid == updIT.individualtaskid);
                if (indIT == -1)
                {
                    throw new InvalidOperationException("Individual Task not found in document.");
                }

                // Step 3: Perform patch to replace the IndividualTask at the correct index
                var patchOps = new List<PatchOperation>
                {
                    PatchOperation.Replace($"/GroupTask/{indGT}/IndividualTaskSets/0/IndividualTask/{indIT}", updIT)
                };

                await container.PatchItemAsync<GroupTaskSet>(
                    id: id,
                    partitionKey: new PartitionKey(tenantid),
                    patchOperations: patchOps
                );

                return response.StatusCode == System.Net.HttpStatusCode.OK;
            }
            catch (CosmosException ex)
            {
                Console.WriteLine($"Error adding Indvidual Task: {ex.StatusCode} - {ex.Message}");
                return false;
            }

        }


    }
}
