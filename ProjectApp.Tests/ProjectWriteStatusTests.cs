using System.Net;
using Newtonsoft.Json.Linq;
using Taslow.Project.DAL;
using Taslow.Project.Model;
using Xunit;

namespace ProjectApp.Tests;

public sealed class ProjectWriteStatusTests
{
    [Fact]
    public void TaskProject_SerializesTenantIdAtCosmosPartitionKeyPath()
    {
        const string tenantId = "tenant-a";
        var document = JObject.FromObject(new TaskProject { tenantid = tenantId });

        Assert.Equal(tenantId, document.Value<string>("tenantID"));
        Assert.Null(document["TenantID"]);
    }

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
