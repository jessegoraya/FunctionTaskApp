using System;
using System.Linq;
using System.Reflection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Taslow.Task.Function;
using Xunit;

namespace FunctionTaskApp.Tests
{
    public class TaskFunctionMetadataTests
    {
        [Fact]
        public void Functions_ShouldPreserveTaskRouteMetadata()
        {
            AssertTrigger("Ping", AuthorizationLevel.Anonymous, "ping", "get");
            AssertTrigger("AddGroupTaskSet", AuthorizationLevel.Function, "grouptaskset", "post");
            AssertTrigger("GetGroupTaskSetById", AuthorizationLevel.Function, "grouptaskset/{id}/{tenantid}", "get");
            AssertTrigger("GetGroupTaskSetByProjectId", AuthorizationLevel.Function, "grouptasksetbyproject/{projectid}/{tenantid}", "get");
            AssertTrigger("GetGroupTaskSetsByProjectId", AuthorizationLevel.Function, "grouptasksetsbyproject/{projectid}/{tenantid}", "get");
            AssertTrigger("UpdateGroupTaskSet", AuthorizationLevel.Function, "grouptaskset/{id}/{tenantid}", "put");
            AssertTrigger("DeleteGroupTaskSet", AuthorizationLevel.Function, "grouptaskset/{id}/{tenantid}", "delete");
            AssertTrigger("AddGroupTaskToGTS", AuthorizationLevel.Function, "addgrouptasktogts/{id}/{tenantid}/", "post");
            AssertTrigger("UpdateGroupTaskinGTS", AuthorizationLevel.Function, "updgrouptask/{id}/{tenantid}/", "post");
            AssertTrigger("AddIndividualTaskToGT", AuthorizationLevel.Function, "addindtask/{id}/{tenantid}/{gtid}/", "post");
            AssertTrigger("UpdateIndividualTaskinGT", AuthorizationLevel.Function, "updindtask/{id}/{tenantid}/{gtid}/", "post");
            AssertTrigger("MoveIndividualTask", AuthorizationLevel.Function, "moveindtask/{tenantid}/", "post");
            AssertTrigger("GetGTContextDTObyTenantandPerson", AuthorizationLevel.Function, "taskcontextdto/{tenantid}/{person}", "get");
            AssertTrigger("GetTasksForManagedProjects", AuthorizationLevel.Function, "getmgrtaskcontextdto/{tenantid}/{manager}", "get");
            AssertTrigger("GetAnalyticsPortfolio", AuthorizationLevel.Function, "analytics/{tenantId}/portfolio", "get");
            AssertTrigger("GetAnalyticsProjectType", AuthorizationLevel.Function, "analytics/{tenantId}/project-types/{projectType}", "get");
            AssertTrigger("GetAnalyticsProjectHierarchy", AuthorizationLevel.Function, "analytics/{tenantId}/projects/{projectId}/hierarchy", "get");
        }

        [Fact]
        public void Functions_ShouldExposeExpectedFunctionCount()
        {
            var count = typeof(FunctionTaskController)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Count(method => method.GetCustomAttribute<FunctionAttribute>() != null);

            Assert.Equal(17, count);
        }

        private static void AssertTrigger(
            string functionName,
            AuthorizationLevel authLevel,
            string route,
            string method)
        {
            var function = typeof(FunctionTaskController)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .SingleOrDefault(candidate =>
                    string.Equals(
                        candidate.GetCustomAttribute<FunctionAttribute>()?.Name,
                        functionName,
                        StringComparison.Ordinal));

            Assert.NotNull(function);

            var trigger = function!
                .GetParameters()
                .Select(parameter => parameter.GetCustomAttribute<HttpTriggerAttribute>())
                .Single(attribute => attribute != null);

            Assert.Equal(authLevel, trigger!.AuthLevel);
            Assert.Equal(route, trigger.Route);
            Assert.Contains(method, trigger.Methods, StringComparer.OrdinalIgnoreCase);
        }
    }
}
