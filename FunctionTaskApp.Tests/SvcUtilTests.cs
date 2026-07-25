using System;
using System.Collections.Generic;
using Taslow.Task.Model;
using Taslow.Task.Service;
using Xunit;

namespace FunctionTaskApp.Tests;

public class SvcUtilTests
{
    [Fact]
    public void SetNewIDs_ShouldPreserveCallerAssignedGroupTaskId()
    {
        var expectedId = "88ea947e-ac18-5971-81b8-0ca959a4d3ba";
        var task = new GroupTask
        {
            grouptaskid = expectedId,
            individualtasksets = new List<IndividualTaskSet>()
        };

        var result = new SvcUtil().SetNewIDs(task);

        Assert.Equal(expectedId, result.grouptaskid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void SetNewIDs_ShouldGenerateGroupTaskIdWhenCallerDidNotAssignOne(string input)
    {
        var task = new GroupTask
        {
            grouptaskid = input,
            individualtasksets = new List<IndividualTaskSet>()
        };

        var result = new SvcUtil().SetNewIDs(task);

        Assert.True(Guid.TryParse(result.grouptaskid, out var generatedId));
        Assert.NotEqual(Guid.Empty, generatedId);
    }
}
