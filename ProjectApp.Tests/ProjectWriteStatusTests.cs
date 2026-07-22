using System.Net;
using Taslow.Project.DAL;
using Xunit;

namespace ProjectApp.Tests;

public sealed class ProjectWriteStatusTests
{
    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Created)]
    [InlineData(HttpStatusCode.Accepted)]
    [InlineData(HttpStatusCode.NoContent)]
    public void IsSuccessfulWriteStatus_AcceptsTwoHundredResponses(HttpStatusCode statusCode)
    {
        Assert.True(DBUtil.IsSuccessfulWriteStatus(statusCode));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public void IsSuccessfulWriteStatus_RejectsNonSuccessResponses(HttpStatusCode statusCode)
    {
        Assert.False(DBUtil.IsSuccessfulWriteStatus(statusCode));
    }
}
