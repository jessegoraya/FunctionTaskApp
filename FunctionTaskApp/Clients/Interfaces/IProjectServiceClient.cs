using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Taslow.Shared.Model;

namespace Taslow.Task.Client.Interface;
    public interface IProjectServiceClient
{
    Task<Dictionary<string, ProjectDTO>> GetProjectsAsync(
        List<string> projectIds,
        string tenantId);

    Task<List<string>> GetProjectIdsForManagerAsync(string tenantId, string manager);
}


