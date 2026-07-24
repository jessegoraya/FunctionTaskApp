using Microsoft.Extensions.Configuration;
using Taslow.Tenant.Service;
using Xunit;

namespace TenantApp.Tests;

public class TenantEmailQueueClientTests
{
    [Theory]
    [InlineData(
        "AzureWebJobsStorage:accountName",
        "taslowstorage",
        "https://taslowstorage.queue.core.windows.net")]
    [InlineData(
        "AzureWebJobsStorage__accountName",
        "taslowstorage",
        "https://taslowstorage.queue.core.windows.net")]
    [InlineData(
        "AzureWebJobsStorage:queueServiceUri",
        "https://queue.example.test",
        "https://queue.example.test")]
    [InlineData(
        "AzureWebJobsStorage__queueServiceUri",
        "https://queue.example.test",
        "https://queue.example.test")]
    public void ResolveQueueServiceUriSupportsWorkerAndEnvironmentKeyFormats(
        string key,
        string value,
        string expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [key] = value
            })
            .Build();

        Assert.Equal(expected, TenantEmailQueueClient.ResolveQueueServiceUri(configuration));
    }
}
