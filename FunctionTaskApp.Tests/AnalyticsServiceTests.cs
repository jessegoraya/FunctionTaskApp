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

namespace FunctionTaskApp.Tests
{
    public class AnalyticsServiceTests
    {
        [Fact]
        public async Task GetPortfolioAsync_ShouldRestrictTenantLeaderToLeaderMarketCodes()
        {
            var service = BuildAnalyticsService();

            var result = await service.GetPortfolioAsync(
                "tenant-a",
                "leader@example.com",
                new[] { TenantRoles.TenantLeader },
                new[] { "MKT-A" },
                Array.Empty<string>());

            Assert.Equal(1, result.Summary.ProjectCount);
            Assert.Equal(1, result.Summary.AtRiskProjectCount);
            Assert.DoesNotContain(result.ByProjectType, item =>
                item.ProjectCount > 0 && item.ProjectType == ProjectTypes.Support);
        }

        [Fact]
        public async Task GetPortfolioAsync_ShouldAllowTenantPmManagedProjects()
        {
            var service = BuildAnalyticsService();

            var result = await service.GetPortfolioAsync(
                "tenant-a",
                "pm@example.com",
                new[] { TenantRoles.TenantPm },
                Array.Empty<string>(),
                Array.Empty<string>());

            Assert.Equal(1, result.Summary.ProjectCount);
            Assert.Equal(1, result.Summary.AtRiskProjectCount);
        }

        [Fact]
        public async Task GetPortfolioAsync_ShouldRejectUnsupportedRoles()
        {
            var service = BuildAnalyticsService();

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                service.GetPortfolioAsync(
                    "tenant-a",
                    "viewer@example.com",
                    new[] { TenantRoles.TenantUser },
                    Array.Empty<string>(),
                    Array.Empty<string>()));
        }

        [Fact]
        public async Task GetProjectTypeAsync_ShouldRejectUnknownProjectType()
        {
            var service = BuildAnalyticsService();

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.GetProjectTypeAsync(
                    "tenant-a",
                    "UnknownType",
                    "admin@example.com",
                    new[] { TenantRoles.TenantAdmin },
                    Array.Empty<string>(),
                    Array.Empty<string>()));
        }

        private static AnalyticsService BuildAnalyticsService()
        {
            var projects = new List<ProjectDTO>
            {
                Project("project-a", ProjectTypes.Delivery, "MKT-A", "pm@example.com", "gts-a"),
                Project("project-b", ProjectTypes.Support, "MKT-B", "other@example.com", "gts-b")
            };

            var taskSets = new Dictionary<string, List<GroupTaskSet>>(StringComparer.OrdinalIgnoreCase)
            {
                ["project-a"] = new() { TaskSet("gts-a", "project-a", overdue: true) },
                ["project-b"] = new() { TaskSet("gts-b", "project-b", overdue: false) }
            };

            return new AnalyticsService(
                new FakeTaskDb(taskSets),
                new FakeProjectServiceClient(projects));
        }

        private static ProjectDTO Project(
            string id,
            string type,
            string marketCode,
            string managerEmail,
            string groupTaskSetId) =>
            new()
            {
                Id = id,
                ProjectName = id,
                ProjectType = type,
                ProjectStatus = "Active",
                MarketCode = marketCode,
                AssociatedManagers = new()
                {
                    new ProjectPersonDTO { PersonEmail = managerEmail, Role = "Manager" }
                },
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

        private static GroupTaskSet TaskSet(string id, string projectId, bool overdue) =>
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
                        grouptaskduedate = new()
                        {
                            new Taslow.Task.Model.GroupTaskDueDate { grouptaskduedate = DateTime.UtcNow.Date.AddDays(7) }
                        },
                        individualtasksets = new()
                        {
                            new IndividualTaskSet
                            {
                                individualtasksetid = $"its-{id}",
                                individualtask = new()
                                {
                                    new IndividualTask
                                    {
                                        individualtaskid = $"it-{id}",
                                        individualtasktitle = $"Task {id}",
                                        individualtasktype = "Review",
                                        individualtaskstatus = "Open",
                                        assignedperson = "pm@example.com",
                                        individualtaskassigneddate = DateTime.UtcNow.Date.AddDays(-10),
                                        individualtaskduedate = overdue
                                            ? DateTime.UtcNow.Date.AddDays(-3)
                                            : DateTime.UtcNow.Date.AddDays(10),
                                        createddate = DateTime.UtcNow.Date.AddDays(-10)
                                    }
                                }
                            }
                        }
                    }
                }
            };

        private sealed class FakeTaskDb : ITaskDBUtil
        {
            private readonly Dictionary<string, List<GroupTaskSet>> _taskSets;

            public FakeTaskDb(Dictionary<string, List<GroupTaskSet>> taskSets)
            {
                _taskSets = taskSets;
            }

            public Task<List<GroupTaskSet>> GetGroupTaskSetsByProjectId(string projectid, string tenantid) =>
                Task.FromResult(_taskSets.TryGetValue(projectid, out var items) ? items : new List<GroupTaskSet>());

            public Task<GroupTaskSet> InsertGroupTaskSet(GroupTaskSet item) => throw new NotSupportedException();
            public Task<GroupTaskSet> GetGroupTaskSet(string id, string tenantid) => throw new NotSupportedException();
            public Task<GroupTaskSet> GetGroupTaskSetByProjectId(string projectid, string tenantid) => throw new NotSupportedException();
            public Task<TaskContextDTO> GetGroupTaskSetByTenantId(string tenantid, string status) => throw new NotSupportedException();
            public Task<List<TaskContextDTO>> GetTasksByProjectIdsAsync(string tenantId, IEnumerable<string> projectIds) => throw new NotSupportedException();
            public Task<List<TaskContextDTO>> GetGTContextDTO(string tenantid, string person) => throw new NotSupportedException();
            public Task<bool> UpdateGroupTaskSet(string id, string tenantid, GroupTaskSet updatedItem) => throw new NotSupportedException();
            public Task<bool> DeleteGroupTaskSet(string id, string tenantid) => throw new NotSupportedException();
            public Task<bool> CreateGroupTaskAsync(string id, string tenantid, GroupTask newGroupTask) => throw new NotSupportedException();
            public Task<bool> UpdateGroupTaskAsync(string id, string tenantid, GroupTask updGT) => throw new NotSupportedException();
            public Task<bool> DeleteGroupTaskAsync(string id, string tenantid, string groupTaskId) => throw new NotSupportedException();
            public Task<bool> CreateIndividualTaskAsync(string id, string tenantid, string gtid, IndividualTask newIndividualTask) => throw new NotSupportedException();
            public Task<bool> UpdateIndividualTaskAsync(string id, string tenantid, string grouptaskid, UpdateIndividualTaskDTO updIT) => throw new NotSupportedException();
            public Task<bool> MoveIndividualTaskAsync(string tenantid, MoveIndividualTaskDTO moveIT) => throw new NotSupportedException();
        }

        private sealed class FakeProjectServiceClient : IProjectServiceClient
        {
            private readonly List<ProjectDTO> _projects;

            public FakeProjectServiceClient(List<ProjectDTO> projects)
            {
                _projects = projects;
            }

            public Task<List<ProjectDTO>> GetProjectsAsync(List<string> projectIds, string tenantId) =>
                Task.FromResult(_projects.Where(project => projectIds.Contains(project.Id)).ToList());

            public Task<List<ProjectDTO>> GetActiveProjectsAsync(string tenantId) =>
                Task.FromResult(_projects.ToList());

            public Task<List<string>> GetProjectIdsForManagerAsync(string tenantId, string manager) =>
                Task.FromResult(_projects
                    .Where(project => project.AssociatedManagers.Any(item =>
                        string.Equals(item.PersonEmail, manager, StringComparison.OrdinalIgnoreCase)))
                    .Select(project => project.Id)
                    .ToList());
        }
    }
}
