using System;
using Taslow.Task.DAL;
using Taslow.Task.Function;
using Taslow.Task.Model;
using Taslow.Shared.Model;
using Xunit;

namespace FunctionTaskApp.Tests;

public sealed class TaskMovePolicyTests
{
    [Fact]
    public void ApplyMoveUpdates_ShouldReplaceAssigneeAndEditableTaskFields()
    {
        var task = new IndividualTask
        {
            individualtaskid = "task-1",
            individualtasktitle = "Original title",
            individualtaskdescription = "Original description",
            assignedperson = "old-owner@example.com",
            individualtaskduedate = new DateTime(2026, 8, 6, 15, 0, 0, DateTimeKind.Utc)
        };
        var move = new MoveIndividualTaskDTO
        {
            individualtasktitle = "Corrected title",
            individualtaskdescription = "Corrected description",
            assignedperson = "new-owner@example.com",
            individualtaskduedate = new DateTime(2026, 8, 12, 17, 0, 0, DateTimeKind.Utc)
        };

        DBUtil.ApplyMoveUpdates(task, move);

        Assert.Equal("Corrected title", task.individualtasktitle);
        Assert.Equal("Corrected description", task.individualtaskdescription);
        Assert.Equal("new-owner@example.com", task.assignedperson);
        Assert.Equal(move.individualtaskduedate, task.individualtaskduedate);
    }

    [Fact]
    public void FindIndividualTask_ShouldSearchAcrossGroupAndTaskSetHierarchy()
    {
        var expected = new IndividualTask { individualtaskid = "task-2" };
        var taskSet = new GroupTaskSet
        {
            grouptask =
            [
                new GroupTask
                {
                    individualtasksets =
                    [
                        new IndividualTaskSet { individualtask = [expected] }
                    ]
                }
            ]
        };

        Assert.Same(expected, FunctionTaskController.FindIndividualTask(taskSet, "task-2"));
    }

    [Fact]
    public void CanMoveTask_ShouldAllowOwnerAndManagedProjectButDenyUnalignedTask()
    {
        var task = new IndividualTask
        {
            individualtaskid = "task-3",
            assignedperson = "owner@example.com"
        };
        var owner = Auth("owner@example.com", TenantRoles.TenantUser);
        var manager = Auth("manager@example.com", TenantRoles.TenantPm);
        var unrelatedMember = Auth("other@example.com", TenantRoles.TenantUser);

        Assert.True(FunctionTaskController.CanMoveTask(
            owner,
            task,
            "project-a",
            Array.Empty<string>()));
        Assert.True(FunctionTaskController.CanMoveTask(
            manager,
            task,
            "project-a",
            new[] { "project-a" }));
        Assert.False(FunctionTaskController.CanMoveTask(
            unrelatedMember,
            task,
            "project-a",
            Array.Empty<string>()));
    }

    private static TaskAuthContext Auth(string email, string role) => new()
    {
        Email = email,
        Roles = [role]
    };
}
