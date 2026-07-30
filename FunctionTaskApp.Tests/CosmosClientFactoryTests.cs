using System;
using Microsoft.Azure.Cosmos;
using Taslow.Shared.Infrastructure;
using Xunit;

namespace FunctionTaskApp.Tests;

public class CosmosClientFactoryTests
{
    [Fact]
    public void Create_uses_connection_string_when_provided()
    {
        var accountKey = Convert.ToBase64String(new byte[64]);
        using var client = CosmosClientFactory.Create(key => key switch
        {
            "CosmosDBConnection" =>
                $"AccountEndpoint=https://local.example.com:443/;AccountKey={accountKey};",
            "CosmosDBConnectionMode" => "Gateway",
            _ => null
        });

        Assert.Equal(new Uri("https://local.example.com:443/"), client.Endpoint);
        Assert.Equal(ConnectionMode.Gateway, client.ClientOptions.ConnectionMode);
    }

    [Fact]
    public void Create_uses_managed_identity_endpoint_when_connection_string_is_absent()
    {
        using var client = CosmosClientFactory.Create(key => key switch
        {
            "CosmosDBEndpoint" => "https://managed.example.com:443/",
            "CosmosDBConnectionMode" => "gateway",
            _ => null
        });

        Assert.Equal(new Uri("https://managed.example.com:443/"), client.Endpoint);
        Assert.Equal(ConnectionMode.Gateway, client.ClientOptions.ConnectionMode);
    }

    [Fact]
    public void Create_rejects_missing_local_and_managed_identity_configuration()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => CosmosClientFactory.Create(_ => null));

        Assert.Contains("CosmosDBEndpoint", exception.Message);
    }

    [Fact]
    public void Create_rejects_an_unknown_connection_mode()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CosmosClientFactory.Create(key => key switch
            {
                "CosmosDBEndpoint" => "https://managed.example.com:443/",
                "CosmosDBConnectionMode" => "Unsupported",
                _ => null
            }));

        Assert.Contains("Direct or Gateway", exception.Message);
    }
}
