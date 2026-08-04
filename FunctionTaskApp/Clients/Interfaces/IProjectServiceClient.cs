using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Taslow.Shared.Model;

namespace Taslow.Task.Client.Interface;
    public interface IProjectServiceClient
{
    Task<List<ProjectDTO>> GetProjectsAsync(
        List<string> projectIds,
        string tenantId,
        string accessToken);

    Task<List<ProjectDTO>> GetActiveProjectsAsync(string tenantId, string accessToken);

    Task<List<TaskProjectOptionDTO>> GetTaskReassignmentProjectsAsync(
        string tenantId,
        string accessToken);

    Task<ProjectAssociationsDTO> GetProjectAssociationsAsync(
        string tenantId,
        string projectId,
        string accessToken);

    Task<List<string>> GetProjectIdsForManagerAsync(string tenantId, string manager, string accessToken);
}


