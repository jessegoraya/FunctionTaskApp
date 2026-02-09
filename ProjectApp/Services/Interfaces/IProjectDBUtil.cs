using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Taslow.Project.Model;
using Taslow.Shared.Model;

namespace Taslow.Project.DAL.Interface
{
    public interface IProjectDBUtil
    {
        Task<bool> InsertProject(TaskProject item);

        Task<Dictionary<string, TaskProject>> GetProjectDatabyProjectIDList(List<string> projectIds, string tenantid);

        Task<List<string>> GetProjectIdsForManagerAsync(string userEmail, string tenantid);

        Task<List<TaskProject>> GetActiveProjectsByTenantAsync(string tenantId);

        Task<object> GetProjectAssociationsAsync(string tenantId, string projectId, string mode, string role);

        Task<Dictionary<string, ProjectDTO>> GetProjectsByIdListAsync(List<string> projectIds, string tenantId);



    }
}
