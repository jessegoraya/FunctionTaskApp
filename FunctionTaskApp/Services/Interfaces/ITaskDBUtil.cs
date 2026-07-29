using System.Collections.Generic;
using System.Threading.Tasks;
using Taslow.Task.Model;
using Taslow.Shared.Model;

namespace Taslow.Task.DAL.Interface;

public interface ITaskDBUtil
{
    Task<GroupTaskSet> InsertGroupTaskSet(GroupTaskSet item);

    Task<GroupTaskSet> GetGroupTaskSet(string id, string tenantid);
    Task<GroupTaskSet> GetGroupTaskSetByProjectId(string projectid, string tenantid);
    Task<List<GroupTaskSet>> GetGroupTaskSetsByProjectId(string projectid, string tenantid);

    Task<TaskContextDTO> GetGroupTaskSetByTenantId(string tenantid, string status);

    Task<List<TaskContextDTO>> GetTasksByProjectIdsAsync(
        string tenantId,
        IEnumerable<string> projectIds,
        string accessToken);

    Task<List<TaskContextDTO>> GetGTContextDTO(
        string tenantid,
        string person,
        string accessToken);

    Task<bool> UpdateGroupTaskSet(string id, string tenantid, GroupTaskSet updatedItem);
    Task<bool> DeleteGroupTaskSet(string id, string tenantid);

    Task<bool> CreateGroupTaskAsync(string id, string tenantid, GroupTask newGroupTask);
    Task<bool> UpdateGroupTaskAsync(string id, string tenantid, GroupTask updGT);
    Task<bool> DeleteGroupTaskAsync(
        string id,
        string tenantid,
        string groupTaskId);

    Task<bool> CreateIndividualTaskAsync(
        string id, string tenantid, string gtid, IndividualTask newIndividualTask);

    Task<bool> UpdateIndividualTaskAsync(
        string id, string tenantid, string grouptaskid, UpdateIndividualTaskDTO updIT);

    Task<bool> MoveIndividualTaskAsync(
        string tenantid, MoveIndividualTaskDTO moveIT);
}
