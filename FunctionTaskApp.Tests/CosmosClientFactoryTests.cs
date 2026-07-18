using System;
using Taslow.Shared.Infrastructure;
using Xunit;

namespace FunctionTaskApp.Tests;

public class CosmosClientFactoryTests
{
    [Fact]
    public void Create_uses_connection_string_when_provided()
    {
        var accountKey = Convert.ToBase64String(new byte[64]);
        using var client = CosmosClientFactory.Create(key => key == "CosmosDBConnection"
            ? $"AccountEndpoint=https://local.example.com:443/;AccountKey={accountKey};"
            : null);

        Assert.Equal(new Uri("https://local.example.com:443/"), client.Endpoint);
    }

    [Fact]
    public void Create_uses_managed_identity_endpoint_when_connection_string_is_absent()
    {
        using var client = CosmosClientFactory.Create(key => key == "CosmosDBEndpoint"
            ? "https://managed.example.com:443/"
            : null);

        Assert.Equal(new Uri("https://managed.example.com:443/"), client.Endpoint);
    }

    [Fact]
    public void Create_rejects_missing_local_and_managed_identity_configuration()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => CosmosClientFactory.Create(_ => null));

        Assert.Contains("CosmosDBEndpoint", exception.Message);
    }
}
