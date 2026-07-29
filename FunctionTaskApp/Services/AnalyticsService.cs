using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Taslow.Shared.Model;
using Taslow.Task.Client.Interface;
using Taslow.Task.DAL.Interface;
using Taslow.Task.Model;
using Taslow.Task.Service.Interface;

namespace Taslow.Task.Service
{
    public class AnalyticsService : IAnalyticsService
    {
        private static readonly HashSet<string> ClosedStatuses = new(
            new[] { "completed", "complete", "closed", "cancelled", "canceled" },
            StringComparer.OrdinalIgnoreCase);

        private readonly ITaskDBUtil _taskDb;
        private readonly IProjectServiceClient _projectClient;

        public AnalyticsService(ITaskDBUtil taskDb, IProjectServiceClient projectClient)
        {
            _taskDb = taskDb;
            _projectClient = projectClient;
        }

        public async Task<AnalyticsPortfolioResponse> GetPortfolioAsync(
            string tenantId,
            string userEmail,
            IReadOnlyCollection<string> roles,
            IReadOnlyCollection<string> leaderMarketCodes,
            IReadOnlyCollection<string> marketCodeFilter,
            string accessToken)
        {
            var visibleProjects = await GetVisibleProjectsAsync(
                tenantId, userEmail, roles, leaderMarketCodes, marketCodeFilter, accessToken);
            var health = await BuildProjectHealthAsync(tenantId, visibleProjects);

            var byType = ProjectTypes.All
                .OrderBy(ProjectTypeSortOrder)
                .Select(projectType =>
                {
                    var projects = health
                        .Where(item => string.Equals(item.ProjectType, projectType, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    var distribution = BuildDistribution(projects);
                    return new AnalyticsProjectTypeHealthDTO
                    {
                        ProjectType = projectType,
                        ProjectCount = distribution.ProjectCount,
                        HealthyProjectCount = distribution.HealthyProjectCount,
                        NeedsAttentionProjectCount = distribution.NeedsAttentionProjectCount,
                        AtRiskProjectCount = distribution.AtRiskProjectCount,
                        NoDataProjectCount = distribution.NoDataProjectCount,
                        HealthStatus = CalculateAggregateHealth(projects),
                        OverdueIndividualTaskCount = projects.Sum(project => project.OverdueIndividualTaskCount),
                        OldestOverdueIndividualTaskAgeDays = MaxNullable(
                            projects.Select(project => project.OldestOverdueIndividualTaskAgeDays))
                    };
                })
                .ToList();

            return new AnalyticsPortfolioResponse
            {
                TenantId = tenantId,
                AsOfUtc = DateTime.UtcNow.ToString("O"),
                Summary = BuildDistribution(health),
                ByProjectType = byType
            };
        }

        public async Task<AnalyticsProjectTypeResponse> GetProjectTypeAsync(
            string tenantId,
            string projectType,
            string userEmail,
            IReadOnlyCollection<string> roles,
            IReadOnlyCollection<string> leaderMarketCodes,
            IReadOnlyCollection<string> marketCodeFilter,
            string accessToken)
        {
            var canonicalType = ProjectTypes.All
                .FirstOrDefault(value => string.Equals(value, projectType, StringComparison.OrdinalIgnoreCase));
            if (canonicalType == null)
            {
                throw new ArgumentException("Project Type must be Administrative, Delivery, Support, or Capture.");
            }

            var visibleProjects = (await GetVisibleProjectsAsync(
                    tenantId, userEmail, roles, leaderMarketCodes, marketCodeFilter, accessToken))
                .Where(project => string.Equals(project.ProjectType, canonicalType, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var health = await BuildProjectHealthAsync(tenantId, visibleProjects);

            return new AnalyticsProjectTypeResponse
            {
                TenantId = tenantId,
                ProjectType = canonicalType,
                HealthStatus = CalculateAggregateHealth(health),
                Summary = BuildDistribution(health),
                ByMarketCode = health
                    .GroupBy(project => string.IsNullOrWhiteSpace(project.MarketCode) ? "Unassigned" : project.MarketCode)
                    .OrderBy(group => group.Key)
                    .Select(group => new AnalyticsMarketDistributionDTO
                    {
                        MarketCode = group.Key,
                        ProjectCount = group.Count(),
                        AtRiskProjectCount = group.Count(project =>
                            project.HealthStatus == AnalyticsHealthStatuses.AtRisk)
                    })
                    .ToList(),
                Projects = health
                    .OrderBy(HealthSortOrder)
                    .ThenByDescending(project => project.OverdueIndividualTaskCount)
                    .ThenBy(project => project.ProjectName)
                    .ToList(),
                ProjectsNeedingAttention = health
                    .Where(project => project.HealthStatus == AnalyticsHealthStatuses.AtRisk ||
                                      project.HealthStatus == AnalyticsHealthStatuses.NeedsAttention)
                    .OrderBy(HealthSortOrder)
                    .ThenByDescending(project => project.OverdueIndividualTaskCount)
                    .Take(5)
                    .Select(project => new AnalyticsProjectAttentionDTO
                    {
                        ProjectId = project.ProjectId,
                        ProjectName = project.ProjectName,
                        MarketCode = project.MarketCode,
                        HealthStatus = project.HealthStatus,
                        Message = BuildAttentionMessage(project)
                    })
                    .ToList()
            };
        }

        public async Task<AnalyticsProjectHierarchyResponse> GetProjectHierarchyAsync(
            string tenantId,
            string projectId,
            string userEmail,
            IReadOnlyCollection<string> roles,
            IReadOnlyCollection<string> leaderMarketCodes,
            string accessToken)
        {
            var visibleProjects = await GetVisibleProjectsAsync(
                tenantId, userEmail, roles, leaderMarketCodes, Array.Empty<string>(), accessToken);
            var project = visibleProjects.FirstOrDefault(item => item.Id == projectId);
            if (project == null)
            {
                throw new KeyNotFoundException("Project was not found or is outside the caller's Analytics scope.");
            }

            var taskSets = FilterTaskSets(project, await _taskDb.GetGroupTaskSetsByProjectId(projectId, tenantId));
            var projectHealth = BuildProjectHealth(project, taskSets);
            var nodes = new List<AnalyticsHierarchyNodeDTO>();
            var exceptions = new List<AnalyticsHierarchyExceptionDTO>();

            foreach (var taskSet in taskSets)
            {
                var linkedScope = (project.Scopes ?? new()).FirstOrDefault(scope =>
                    string.Equals(scope.GroupTaskSetId, taskSet.id, StringComparison.OrdinalIgnoreCase));
                var scopeTitle = FirstNonEmpty(
                    linkedScope?.ProjectScopeAreaTitle,
                    FirstNonEmpty(linkedScope?.ProjectScopeArea, FirstNonEmpty(taskSet.projectscopearea, "Untitled scope")));
                var scopeTasks = EnumerateIndividualTasks(taskSet).ToList();
                var scopeId = $"gts:{taskSet.id}";
                nodes.Add(BuildNode(
                    scopeId,
                    null,
                    "Scope / GTS",
                    0,
                    scopeTitle,
                    null,
                    null,
                    scopeTasks,
                    (taskSet.grouptask ?? new()).Any()));

                foreach (var groupTask in taskSet.grouptask ?? new())
                {
                    var groupTasks = EnumerateIndividualTasks(groupTask).ToList();
                    var groupTaskId = $"gt:{taskSet.id}:{groupTask.grouptaskid}";
                    nodes.Add(BuildNode(
                        groupTaskId,
                        scopeId,
                        "Group Task",
                        1,
                        FirstNonEmpty(groupTask.grouptasktitle, "Untitled group task"),
                        null,
                        LastDueDate(groupTask.grouptaskduedate),
                        groupTasks,
                        (groupTask.individualtasksets ?? new()).Any()));

                    var individualTaskSets = groupTask.individualtasksets ?? new();
                    for (var individualTaskSetIndex = 0; individualTaskSetIndex < individualTaskSets.Count; individualTaskSetIndex++)
                    {
                        var individualTaskSet = individualTaskSets[individualTaskSetIndex];
                        var individualTaskSetName = IndividualTaskSetName(individualTaskSet, individualTaskSetIndex);
                        var individualTasks = individualTaskSet.individualtask ?? new();
                        var individualTaskSetId = $"its:{taskSet.id}:{groupTask.grouptaskid}:{individualTaskSet.individualtasksetid}";
                        nodes.Add(BuildNode(
                            individualTaskSetId,
                            groupTaskId,
                            "Individual Task Set",
                            2,
                            individualTaskSetName,
                            null,
                            null,
                            individualTasks,
                            individualTasks.Any()));

                        foreach (var individualTask in individualTasks)
                        {
                            var individualTaskId = $"it:{taskSet.id}:{groupTask.grouptaskid}:{individualTaskSet.individualtasksetid}:{individualTask.individualtaskid}";
                            nodes.Add(BuildNode(
                                individualTaskId,
                                individualTaskSetId,
                                "Individual Task",
                                3,
                                FirstNonEmpty(individualTask.individualtasktitle, "Untitled individual task"),
                                individualTask.assignedperson,
                                ValidDate(individualTask.individualtaskduedate),
                                new[] { individualTask },
                                false));

                            if (IsOpen(individualTask) && IsOverdue(individualTask, DateTime.UtcNow.Date))
                            {
                                exceptions.Add(new AnalyticsHierarchyExceptionDTO
                                {
                                    IndividualTaskId = individualTask.individualtaskid,
                                    Title = FirstNonEmpty(individualTask.individualtasktitle, "Untitled individual task"),
                                    AssignedPerson = FirstNonEmpty(individualTask.assignedperson, "Unassigned"),
                                    DueDateUtc = individualTask.individualtaskduedate.ToUniversalTime().ToString("O"),
                                    AgeDays = (DateTime.UtcNow.Date - individualTask.individualtaskduedate.Date).Days,
                                    HierarchyPath = string.Join(" / ", new[]
                                    {
                                        scopeTitle,
                                        FirstNonEmpty(groupTask.grouptasktitle, "Untitled group task"),
                                        individualTaskSetName
                                    })
                                });
                            }
                        }
                    }
                }
            }

            return new AnalyticsProjectHierarchyResponse
            {
                TenantId = tenantId,
                Project = projectHealth,
                Nodes = nodes,
                Exceptions = exceptions
                    .OrderByDescending(item => item.AgeDays)
                    .ThenBy(item => item.Title)
                    .ToList()
            };
        }

        private async Task<List<ProjectDTO>> GetVisibleProjectsAsync(
            string tenantId,
            string userEmail,
            IReadOnlyCollection<string> roles,
            IReadOnlyCollection<string> leaderMarketCodes,
            IReadOnlyCollection<string> marketCodeFilter,
            string accessToken)
        {
            var normalizedRoles = new HashSet<string>(roles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            if (!normalizedRoles.Overlaps(new[] { "tenant_pm", "tenant_leader", "tenant_admin", "taslow_admin" }))
            {
                throw new UnauthorizedAccessException("Analytics is available to tenant PM and tenant leader roles.");
            }

            var projects = await _projectClient.GetActiveProjectsAsync(tenantId, accessToken);
            var visibleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (normalizedRoles.Contains("tenant_admin") || normalizedRoles.Contains("taslow_admin"))
            {
                visibleIds.UnionWith(projects.Select(project => project.Id));
            }

            if (normalizedRoles.Contains("tenant_pm") && !string.IsNullOrWhiteSpace(userEmail))
            {
                visibleIds.UnionWith(await _projectClient.GetProjectIdsForManagerAsync(tenantId, userEmail, accessToken));
            }

            if (normalizedRoles.Contains("tenant_leader"))
            {
                var allowedMarkets = new HashSet<string>(leaderMarketCodes ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
                visibleIds.UnionWith(projects
                    .Where(project => allowedMarkets.Contains(project.MarketCode ?? string.Empty))
                    .Select(project => project.Id));
            }

            var requestedMarkets = new HashSet<string>(marketCodeFilter ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            return projects
                .Where(project => visibleIds.Contains(project.Id))
                .Where(project => requestedMarkets.Count == 0 || requestedMarkets.Contains(project.MarketCode ?? string.Empty))
                .ToList();
        }

        private async Task<List<AnalyticsProjectHealthDTO>> BuildProjectHealthAsync(
            string tenantId,
            IReadOnlyCollection<ProjectDTO> projects)
        {
            var work = projects.Select(async project =>
            {
                var taskSets = await _taskDb.GetGroupTaskSetsByProjectId(project.Id, tenantId);
                return BuildProjectHealth(project, taskSets);
            });
            return (await System.Threading.Tasks.Task.WhenAll(work)).ToList();
        }

        private static AnalyticsProjectHealthDTO BuildProjectHealth(ProjectDTO project, IEnumerable<GroupTaskSet> taskSets)
        {
            var activeTaskSets = FilterTaskSets(project, taskSets);
            var individualTasks = activeTaskSets.SelectMany(EnumerateIndividualTasks).ToList();
            var openTasks = individualTasks.Where(IsOpen).ToList();
            var today = DateTime.UtcNow.Date;
            var overdueTasks = openTasks.Where(task => IsOverdue(task, today)).ToList();
            var dueSoonTasks = openTasks.Where(task => IsDueSoon(task, today)).ToList();
            var lastActivity = individualTasks.Select(LastActivity).Where(value => value.HasValue).Max();

            var result = new AnalyticsProjectHealthDTO
            {
                ProjectId = project.Id,
                ProjectName = project.ProjectName,
                ProjectType = project.ProjectType,
                MarketCode = project.MarketCode,
                ManagerEmails = (project.AssociatedManagers ?? new())
                    .Select(manager => manager.PersonEmail)
                    .Where(email => !string.IsNullOrWhiteSpace(email))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                ScopeCount = activeTaskSets.Count,
                GroupTaskCount = activeTaskSets.Sum(item => item.grouptask?.Count ?? 0),
                OpenIndividualTaskCount = openTasks.Count,
                OverdueIndividualTaskCount = overdueTasks.Count,
                DueSoonIndividualTaskCount = dueSoonTasks.Count,
                OldestOpenIndividualTaskAgeDays = MaxNullable(openTasks
                    .Select(task => ValidDate(task.individualtaskassigneddate) ?? ValidDate(task.createddate))
                    .Where(value => value.HasValue)
                    .Select(value => (int?)(today - value.Value.Date).Days)),
                OldestOverdueIndividualTaskAgeDays = MaxNullable(overdueTasks
                    .Select(task => (int?)(today - task.individualtaskduedate.Date).Days)),
                LastTaskActivityUtc = lastActivity?.ToUniversalTime().ToString("O")
            };

            result.HealthStatus = CalculateProjectHealth(result, individualTasks.Count, today, lastActivity);
            return result;
        }

        private static string CalculateProjectHealth(
            AnalyticsProjectHealthDTO project,
            int individualTaskCount,
            DateTime today,
            DateTime? lastActivity)
        {
            if (individualTaskCount == 0)
            {
                return AnalyticsHealthStatuses.NoData;
            }

            var overdueRate = project.OpenIndividualTaskCount == 0
                ? 0
                : (double)project.OverdueIndividualTaskCount / project.OpenIndividualTaskCount;
            var staleDays = lastActivity.HasValue ? (today - lastActivity.Value.Date).Days : int.MaxValue;
            if (overdueRate >= 0.20 || project.OldestOverdueIndividualTaskAgeDays >= 14 || staleDays >= 14)
            {
                return AnalyticsHealthStatuses.AtRisk;
            }

            var dueSoonRate = project.OpenIndividualTaskCount == 0
                ? 0
                : (double)project.DueSoonIndividualTaskCount / project.OpenIndividualTaskCount;
            if (project.OverdueIndividualTaskCount > 0 || dueSoonRate >= 0.25 || staleDays >= 7)
            {
                return AnalyticsHealthStatuses.NeedsAttention;
            }

            return AnalyticsHealthStatuses.Healthy;
        }

        private static AnalyticsHealthDistributionDTO BuildDistribution(IEnumerable<AnalyticsProjectHealthDTO> projects)
        {
            var list = projects.ToList();
            return new AnalyticsHealthDistributionDTO
            {
                ProjectCount = list.Count,
                HealthyProjectCount = list.Count(item => item.HealthStatus == AnalyticsHealthStatuses.Healthy),
                NeedsAttentionProjectCount = list.Count(item => item.HealthStatus == AnalyticsHealthStatuses.NeedsAttention),
                AtRiskProjectCount = list.Count(item => item.HealthStatus == AnalyticsHealthStatuses.AtRisk),
                NoDataProjectCount = list.Count(item => item.HealthStatus == AnalyticsHealthStatuses.NoData)
            };
        }

        private static string CalculateAggregateHealth(IReadOnlyCollection<AnalyticsProjectHealthDTO> projects)
        {
            var reportable = projects.Where(item => item.HealthStatus != AnalyticsHealthStatuses.NoData).ToList();
            if (reportable.Count == 0)
            {
                return AnalyticsHealthStatuses.NoData;
            }

            if ((double)reportable.Count(item => item.HealthStatus == AnalyticsHealthStatuses.AtRisk) / reportable.Count >= 0.25)
            {
                return AnalyticsHealthStatuses.AtRisk;
            }

            if (reportable.Any(item => item.HealthStatus == AnalyticsHealthStatuses.AtRisk ||
                                       item.HealthStatus == AnalyticsHealthStatuses.NeedsAttention))
            {
                return AnalyticsHealthStatuses.NeedsAttention;
            }

            return AnalyticsHealthStatuses.Healthy;
        }

        private static AnalyticsHierarchyNodeDTO BuildNode(
            string id,
            string parentId,
            string level,
            int depth,
            string title,
            string assignee,
            DateTime? dueDate,
            IEnumerable<IndividualTask> tasks,
            bool hasChildren)
        {
            var list = tasks.ToList();
            var open = list.Where(IsOpen).ToList();
            var today = DateTime.UtcNow.Date;
            var activities = list.Select(LastActivity).Where(value => value.HasValue).ToList();
            var lastActivity = activities.Count == 0 ? null : activities.Max();
            return new AnalyticsHierarchyNodeDTO
            {
                Id = id,
                ParentId = parentId,
                Level = level,
                Depth = depth,
                Title = title,
                Assignee = assignee,
                DueDateUtc = dueDate?.ToUniversalTime().ToString("O"),
                OpenCount = open.Count,
                OverdueCount = open.Count(task => IsOverdue(task, today)),
                DueSoonCount = open.Count(task => IsDueSoon(task, today)),
                OldestOpenAgeDays = MaxNullable(open
                    .Select(task => ValidDate(task.individualtaskassigneddate) ?? ValidDate(task.createddate))
                    .Where(value => value.HasValue)
                    .Select(value => (int?)(today - value.Value.Date).Days)),
                LastActivityUtc = lastActivity?.ToUniversalTime().ToString("O"),
                HasChildren = hasChildren
            };
        }

        private static IEnumerable<IndividualTask> EnumerateIndividualTasks(GroupTaskSet taskSet) =>
            (taskSet.grouptask ?? new()).SelectMany(EnumerateIndividualTasks);

        private static IEnumerable<IndividualTask> EnumerateIndividualTasks(GroupTask groupTask) =>
            (groupTask.individualtasksets ?? new()).SelectMany(taskSet => taskSet.individualtask ?? new());

        private static List<GroupTaskSet> FilterTaskSets(ProjectDTO project, IEnumerable<GroupTaskSet> taskSets)
        {
            var active = taskSets.Where(item => !item.isarchived).ToList();
            var linkedIds = new HashSet<string>(
                (project.Scopes ?? new())
                    .Select(scope => scope.GroupTaskSetId)
                    .Where(id => !string.IsNullOrWhiteSpace(id)),
                StringComparer.OrdinalIgnoreCase);

            return linkedIds.Count == 0
                ? active
                : active.Where(item => linkedIds.Contains(item.id)).ToList();
        }

        private static bool IsOpen(IndividualTask task) =>
            !ClosedStatuses.Contains(task.individualtaskstatus ?? string.Empty);

        private static bool IsOverdue(IndividualTask task, DateTime today) =>
            ValidDate(task.individualtaskduedate).HasValue && task.individualtaskduedate.Date < today;

        private static bool IsDueSoon(IndividualTask task, DateTime today) =>
            ValidDate(task.individualtaskduedate).HasValue &&
            task.individualtaskduedate.Date >= today &&
            task.individualtaskduedate.Date <= today.AddDays(7);

        private static DateTime? LastActivity(IndividualTask task)
        {
            var dates = new[]
            {
                ValidDate(task.createddate),
                ValidDate(task.individualtaskassigneddate),
                ValidDate(task.individualtaskcompleteddate)
            }.Where(value => value.HasValue).Select(value => value.Value).ToList();
            return dates.Count == 0 ? null : dates.Max();
        }

        private static DateTime? ValidDate(DateTime value) => value.Year > 1900 ? value : null;

        private static DateTime? LastDueDate(IEnumerable<Taslow.Task.Model.GroupTaskDueDate> dates) =>
            (dates ?? Array.Empty<Taslow.Task.Model.GroupTaskDueDate>())
                .Select(item => ValidDate(item.lastgrouptaskduedate) ?? ValidDate(item.grouptaskduedate))
                .Where(value => value.HasValue)
                .Select(value => value.Value)
                .DefaultIfEmpty()
                .Max() is var maximum && maximum.Year > 1900 ? maximum : null;

        private static int? MaxNullable(IEnumerable<int?> values)
        {
            var materialized = values.Where(value => value.HasValue).Select(value => value.Value).ToList();
            return materialized.Count == 0 ? null : materialized.Max();
        }

        private static int HealthSortOrder(AnalyticsProjectHealthDTO project) => project.HealthStatus switch
        {
            AnalyticsHealthStatuses.AtRisk => 0,
            AnalyticsHealthStatuses.NeedsAttention => 1,
            AnalyticsHealthStatuses.Healthy => 2,
            _ => 3
        };

        private static int ProjectTypeSortOrder(string projectType) => projectType switch
        {
            ProjectTypes.Delivery => 0,
            ProjectTypes.Capture => 1,
            ProjectTypes.Support => 2,
            ProjectTypes.Administrative => 3,
            _ => 4
        };

        private static string BuildAttentionMessage(AnalyticsProjectHealthDTO project)
        {
            if (project.OverdueIndividualTaskCount > 0)
            {
                return $"{project.OverdueIndividualTaskCount} overdue individual task" +
                       (project.OverdueIndividualTaskCount == 1 ? string.Empty : "s");
            }

            if (project.DueSoonIndividualTaskCount > 0)
            {
                return $"{project.DueSoonIndividualTaskCount} individual task" +
                       (project.DueSoonIndividualTaskCount == 1 ? " is" : "s are") + " due within 7 days";
            }

            return project.LastTaskActivityUtc == null
                ? "No recent individual task activity"
                : "Individual task activity is stale";
        }

        private static string FirstNonEmpty(string value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value;

        private static string IndividualTaskSetName(IndividualTaskSet taskSet, int index) =>
            string.IsNullOrWhiteSpace(taskSet.individualtasksetname)
                ? index == 0 ? "Initial" : "Follow-On"
                : taskSet.individualtasksetname;
    }
}
