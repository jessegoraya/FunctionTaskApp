using System;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using CMaaS.Task.Model;
using System.Linq;
using System.Collections.Generic;
using CMaaS.TaskProject.Model;
using CMaaS.TaskProject.DAL;

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

        public async Task<TaskContextDTO> GetGroupTaskSetByTenantId(string tenantid, string status)
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.TenantID = @tenantid AND c.Status = @status")
                .WithParameter("@status", status)
                .WithParameter("@tenantid", tenantid);


            using (FeedIterator<TaskContextDTO> resultSet = container.GetItemQueryIterator<TaskContextDTO>(
                query,
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(tenantid)
                }))
            {
                while (resultSet.HasMoreResults)
                {
                    FeedResponse<TaskContextDTO> response = await resultSet.ReadNextAsync();
                    TaskContextDTO item = response.FirstOrDefault();
                    if (item != null)
                    {
                        return item;
                    }
                }
            }

            return null; // or throw an exception if you want to require a match
        }

        public async Task<List<TaskContextDTO>> GetGTContextDTO(string tenantid, string person)
        {
            string strQuery =
                "SELECT " +
                "    c.ProjectID, " +
                "    c.TenantID, " +
                "    c.id, " +
                "    c.ExtProjectID, " +
                "    g.GroupTaskID, " +
                "    g.GroupTaskTitle, " +
                "    g.GroupTaskDescription, " +
                "    g.GroupTaskStatus, " +
                "    MAX(d.LastGroupTaskDueDate) AS GroupTaskDueDate, " +
                "    g.GroupTaskClosedDate, " +
                "    g.AssociatedDocuments, " +
                "    g.AssociatedLOBItems, " +
                "    g.GroupTaskType, " +
                "    g.GroupTaskStage, " +
                "    g.AssignorStakeholderGroup, " +
                "    a.AssigneeStakeholderGroup, " +
                "    g.GroupTaskNotes, " +
                "    g.FacilitiationComplete, " +
                "    g.FacilitiationPreviouslyComplete, " +
                "    g.CancellationSent, " +
                "    g.ParentGroupTaskID, " +
                "    g.CreatedBy, " +
                "    g.CreatedDate, " +
                "    g.LastModifiedBy, " +
                "    g.LastModifiedDate, " +
                "    s.IndividualTaskSetID, " +
                "    s.ITSCreatedBy, " +
                "    s.ITSCreatedDate, " +
                "    i.IndividualTaskID, " +
                "    i.IndividualTaskStatus, " +
                "    i.IndividualTaskTitle, " +
                "    i.IndividualTaskType, " +
                "    i.IndividualTaskDescription, " +
                "    i.IndividualTaskNotes, " +
                "    i.Priority, " +
                "    i.AssignedPerson, " +
                "    i.AssociatedRole, " +
                "    i.PreviouslySent, " +
                "    i.IndividualTaskAssignedDate, " +
                "    i.IndividualTaskDueDate, " +
                "    i.IndividualTaskCancelledDate, " +
                "    i.IndividualTaskApprovalDecision, " +
                "    i.IndividualTaskCompletedDate, " +
                "    i.ITCreatedBy, " +
                "    i.ITCreatedDate " +
                "FROM c " +
                "JOIN g IN c.GroupTask " +
                "JOIN s IN g.IndividualTaskSets " +
                "JOIN i IN s.IndividualTask " +
                "JOIN d IN g.GroupTaskDueDate " +
                "JOIN a IN g.AssigneeStakeholderGroup " +
                "WHERE i.AssignedPerson = @person " +
                "GROUP BY " +
                "    c.ProjectID, " +
                "    c.TenantID, " +
                "    c.id, " +
                "    c.ExtProjectID, " +
                "    g.GroupTaskID, " +
                "    g.GroupTaskTitle, " +
                "    g.GroupTaskDescription, " +
                "    g.GroupTaskStatus, " +
                "    g.GroupTaskClosedDate, " +
                "    g.AssociatedDocuments, " +
                "    g.AssociatedLOBItems, " +
                "    g.GroupTaskType, " +
                "    g.GroupTaskStage, " +
                "    g.AssignorStakeholderGroup, " +
                "    a.AssigneeStakeholderGroup, " +
                "    g.GroupTaskNotes, " +
                "    g.FacilitiationComplete, " +
                "    g.FacilitiationPreviouslyComplete, " +
                "    g.CancellationSent, " +
                "    g.ParentGroupTaskID, " +
                "    g.CreatedBy, " +
                "    g.CreatedDate, " +
                "    g.LastModifiedBy, " +
                "    g.LastModifiedDate, " +
                "    s.IndividualTaskSetID, " +
                "    s.ITSCreatedBy, " +
                "    s.ITSCreatedDate, " +
                "    i.IndividualTaskID, " +
                "    i.IndividualTaskStatus, " +
                "    i.IndividualTaskTitle, " +
                "    i.IndividualTaskType, " +
                "    i.IndividualTaskDescription, " +
                "    i.IndividualTaskNotes, " +
                "    i.Priority, " +
                "    i.AssignedPerson, " +
                "    i.AssociatedRole, " +
                "    i.PreviouslySent, " +
                "    i.IndividualTaskAssignedDate, " +
                "    i.IndividualTaskDueDate, " +
                "    i.IndividualTaskCancelledDate, " +
                "    i.IndividualTaskApprovalDecision, " +
                "    i.IndividualTaskCompletedDate, " +
                "    i.ITCreatedBy, " +
                "    i.ITCreatedDate ";

            var query = new QueryDefinition(strQuery)
                .WithParameter("@person", person);

            try
            {
                List<TaskContextDTO> results = new List<TaskContextDTO>();

                using (FeedIterator<TaskContextDTO> resultSet = container.GetItemQueryIterator<TaskContextDTO>(
                    query,
                    requestOptions: new QueryRequestOptions
                    {
                        PartitionKey = new PartitionKey(tenantid)
                    }))
                {
                    while (resultSet.HasMoreResults)
                    {
                        FeedResponse<TaskContextDTO> response = await resultSet.ReadNextAsync();
                        results.AddRange(response);
                    }
                }

                // Enrich results with project info
                List<string> projectIds = results
                    .Where(r => !string.IsNullOrEmpty(r.projectid))
                    .Select(r => r.projectid)
                    .Distinct()
                    .ToList();

                //Call Project app DBUtil to return project data based on the project ids from the GTS list
                var projectLookup = new Dictionary<string, Project>();
                var projectDBUtil = new CMaaS.TaskProject.DAL.DBUtil();

                projectLookup = await projectDBUtil.GetProjectDatabyProjectIDList(projectIds, tenantid);

                foreach (var task in results)
                {
                    if (task.projectid != null && projectLookup.TryGetValue(task.projectid, out var project))
                    {
                        task.projectname = project.ProjectNames;
                        task.projectdescription = project.projectdescription;
                        task.projecttype = project.projecttype;
                        task.projectstatus = project.projectstatus;
                    }
                }

                return results;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
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
