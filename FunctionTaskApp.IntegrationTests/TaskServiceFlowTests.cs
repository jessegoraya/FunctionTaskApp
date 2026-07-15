using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Taslow.Shared.Model;
using Taslow.Task.Client.Interface;
using Taslow.Task.DAL.Interface;
using Taslow.Task.Model;
using Taslow.Task.Service;
using Xunit;

namespace FunctionTaskApp.IntegrationTests
{
    public class TaskServiceFlowTests
    {
        [Fact]
        public async Task AnalyticsHierarchyFlow_ShouldReturnVisibleProjectHierarchyAndExceptions()
        {
            var taskDb = new InMemoryTaskDb();
            var projectClient = new InMemoryProjectClient();

            var taskSet = BuildTaskSet("gts-a", "project-a", overdue: true);
            await taskDb.InsertGroupTaskSet(taskSet);
            projectClient.Projects.Add(BuildProject("project-a", "gts-a", ProjectTypes.Delivery, "MKT-A"));

            var service = new AnalyticsService(taskDb, projectClient);

            var result = await service.GetProjectHierarchyAsync(
                "tenant-a",
                "project-a",
                "admin@example.com",
                new[] { TenantRoles.TenantAdmin },
                Array.Empty<string>());

            Assert.Equal("tenant-a", result.TenantId);
            Assert.Equal("project-a", result.Project.ProjectId);
            Assert.Contains(result.Nodes, node => node.Level == "Scope / GTS");
            Assert.Contains(result.Nodes, node => node.Level == "Individual Task");
            Assert.Single(result.Exceptions);
        }

        [Fact]
        public async Task TaskMutationFlow_ShouldMoveIndividualTaskAcrossProjectTaskSets()
        {
            var taskDb = new InMemoryTaskDb();
            await taskDb.InsertGroupTaskSet(BuildTaskSet("source-gts", "project-a", overdue: false));
            await taskDb.InsertGroupTaskSet(BuildTaskSet("target-gts", "project-b", overdue: false, individualTaskId: "target-existing"));

            var moved = await taskDb.MoveIndividualTaskAsync(
                "tenant-a",
                new MoveIndividualTaskDTO
                {
                    individualtaskid = "it-source-gts",
                    sourceprojectid = "project-a",
                    targetprojectid = "project-b",
                    targetgrouptaskid = "gt-target-gts",
                    targetindividualtasksetid = "its-target-gts"
                });

            Assert.True(moved);
            Assert.DoesNotContain(
                (await taskDb.GetGroupTaskSetByProjectId("project-a", "tenant-a"))
                    .grouptask.SelectMany(gt => gt.individualtasksets).SelectMany(its => its.individualtask),
                task => task.individualtaskid == "it-source-gts");
            Assert.Contains(
                (await taskDb.GetGroupTaskSetByProjectId("project-b", "tenant-a"))
                    .grouptask.SelectMany(gt => gt.individualtasksets).SelectMany(its => its.individualtask),
                task => task.individualtaskid == "it-source-gts");
        }

        private static ProjectDTO BuildProject(
            string id,
            string groupTaskSetId,
            string projectType,
            string marketCode) =>
            new()
            {
                Id = id,
                ProjectName = id,
                ProjectType = projectType,
                ProjectStatus = "Active",
                MarketCode = marketCode,
                ProjectScopes = new()
                {
                    new ProjectScopeDTO
                    {
                        ScopeId = $"scope-{id}",
                        ProjectScopeAreaTitle = $"Scope {id}",
                        GroupTaskSetId = groupTaskSetId
                    }
                }
            };

        private static GroupTaskSet BuildTaskSet(
            string id,
            string projectId,
            bool overdue,
            string individualTaskId = null) =>
            new()
            {
                id = id,
                tenantid = "tenant-a",
                caseid = projectId,
                grouptask = new()
                {
                    new GroupTask
                    {
                        grouptaskid = $"gt-{id}",
                        grouptasktitle = $"Group {id}",
                        individualtasksets = new()
                        {
                            new IndividualTaskSet
                            {
                                individualtasksetid = $"its-{id}",
                                individualtasksetname = "Initial",
                                individualtask = new()
                                {
                                    new IndividualTask
                                    {
                                        individualtaskid = individualTaskId ?? $"it-{id}",
                                        individualtasktitle = $"Task {id}",
                                        individualtasktype = "Review",
                                        individualtaskstatus = "Open",
                                        assignedperson = "owner@example.com",
                                        individualtaskassigneddate = DateTime.UtcNow.Date.AddDays(-5),
                                        individualtaskduedate = overdue
                                            ? DateTime.UtcNow.Date.AddDays(-2)
                                            : DateTime.UtcNow.Date.AddDays(10),
                                        createddate = DateTime.UtcNow.Date.AddDays(-5)
                                    }
                                }
                            }
                        }
                    }
                }
            };

        private sealed class InMemoryTaskDb : ITaskDBUtil
        {
            private readonly List<GroupTaskSet> _items = new();

            public Task<GroupTaskSet> InsertGroupTaskSet(GroupTaskSet item)
            {
                _items.Add(Clone(item));
                return Task.FromResult(Clone(item));
            }

            public Task<GroupTaskSet> GetGroupTaskSet(string id, string tenantid) =>
                Task.FromResult(Clone(_items.FirstOrDefault(item =>
                    item.id == id && item.tenantid == tenantid)));

            public Task<GroupTaskSet> GetGroupTaskSetByProjectId(string projectid, string tenantid)
            {
                var items = _items
                    .Where(item => item.caseid == projectid && item.tenantid == tenantid)
                    .ToList();
                return Task.FromResult(Clone(items.FirstOrDefault(item => !item.isarchived) ?? items.FirstOrDefault()));
            }

            public Task<List<GroupTaskSet>> GetGroupTaskSetsByProjectId(string projectid, string tenantid) =>
                Task.FromResult(_items
                    .Where(item => item.caseid == projectid && item.tenantid == tenantid)
                    .Select(Clone)
                    .ToList());

            public Task<bool> MoveIndividualTaskAsync(string tenantid, MoveIndividualTaskDTO moveIT)
            {
                var source = _items.First(item => item.tenantid == tenantid && item.caseid == moveIT.sourceprojectid);
                var target = _items.First(item => item.tenantid == tenantid && item.caseid == moveIT.targetprojectid);

                var sourceGroup = source.grouptask.First(gt =>
                    string.IsNullOrWhiteSpace(moveIT.sourcegrouptaskid) ||
                    gt.grouptaskid == moveIT.sourcegrouptaskid ||
                    gt.individualtasksets.Any(its => its.individualtask.Any(it => it.individualtaskid == moveIT.individualtaskid)));
                var sourceSet = sourceGroup.individualtasksets.First(its =>
                    string.IsNullOrWhiteSpace(moveIT.sourceindividualtasksetid) ||
                    its.individualtasksetid == moveIT.sourceindividualtasksetid ||
                    its.individualtask.Any(it => it.individualtaskid == moveIT.individualtaskid));
                var task = sourceSet.individualtask.First(it => it.individualtaskid == moveIT.individualtaskid);

                sourceSet.individualtask.Remove(task);

                var targetGroup = target.grouptask.First(gt =>
                    string.IsNullOrWhiteSpace(moveIT.targetgrouptaskid) ||
                    gt.grouptaskid == moveIT.targetgrouptaskid);
                var targetSet = targetGroup.individualtasksets.First(its =>
                    string.IsNullOrWhiteSpace(moveIT.targetindividualtasksetid) ||
                    its.individualtasksetid == moveIT.targetindividualtasksetid);

                targetSet.individualtask.Add(task);
                return Task.FromResult(true);
            }

            public Task<TaskContextDTO> GetGroupTaskSetByTenantId(string tenantid, string status) => throw new NotSupportedException();
            public Task<List<TaskContextDTO>> GetTasksByProjectIdsAsync(string tenantId, IEnumerable<string> projectIds) => throw new NotSupportedException();
            public Task<List<TaskContextDTO>> GetGTContextDTO(string tenantid, string person) => throw new NotSupportedException();
            public Task<bool> UpdateGroupTaskSet(string id, string tenantid, GroupTaskSet updatedItem) => throw new NotSupportedException();
            public Task<bool> DeleteGroupTaskSet(string id, string tenantid) => throw new NotSupportedException();
            public Task<bool> CreateGroupTaskAsync(string id, string tenantid, GroupTask newGroupTask) => throw new NotSupportedException();
            public Task<bool> UpdateGroupTaskAsync(string id, string tenantid, GroupTask updGT) => throw new NotSupportedException();
            public Task<bool> CreateIndividualTaskAsync(string id, string tenantid, string gtid, IndividualTask newIndividualTask) => throw new NotSupportedException();
            public Task<bool> UpdateIndividualTaskAsync(string id, string tenantid, string grouptaskid, UpdateIndividualTaskDTO updIT) => throw new NotSupportedException();

            private static GroupTaskSet Clone(GroupTaskSet source)
            {
                if (source == null)
                {
                    return null;
                }

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(source);
                return Newtonsoft.Json.JsonConvert.DeserializeObject<GroupTaskSet>(json);
            }
        }

        private sealed class InMemoryProjectClient : IProjectServiceClient
        {
            public List<ProjectDTO> Projects { get; } = new();

            public Task<List<ProjectDTO>> GetProjectsAsync(List<string> projectIds, string tenantId) =>
                Task.FromResult(Projects.Where(project => projectIds.Contains(project.Id)).ToList());

            public Task<List<ProjectDTO>> GetActiveProjectsAsync(string tenantId) =>
                Task.FromResult(Projects.ToList());

            public Task<List<string>> GetProjectIdsForManagerAsync(string tenantId, string manager) =>
                Task.FromResult(Projects
                    .Where(project => project.AssociatedManagers.Any(item =>
                        string.Equals(item.PersonEmail, manager, StringComparison.OrdinalIgnoreCase)))
                    .Select(project => project.Id)
                    .ToList());
        }
    }
}
