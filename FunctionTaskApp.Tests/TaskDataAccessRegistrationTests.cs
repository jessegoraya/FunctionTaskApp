using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using Taslow.Task.DAL;
using Taslow.Task.DAL.Interface;
using Xunit;

namespace FunctionTaskApp.Tests;

public class TaskDataAccessRegistrationTests
{
    [Fact]
    public void AddTaskDataAccess_reuses_one_Cosmos_client_across_scoped_repositories()
    {
        var services = new ServiceCollection();

        services.AddTaskDataAccess();

        var cosmosRegistration = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(CosmosClient));
        var repositoryRegistration = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(ITaskDBUtil));

        Assert.Equal(ServiceLifetime.Singleton, cosmosRegistration.Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, repositoryRegistration.Lifetime);
        Assert.NotNull(cosmosRegistration.ImplementationFactory);
        Assert.Equal(typeof(DBUtil), repositoryRegistration.ImplementationType);
    }

    [Fact]
    public void DBUtil_requires_the_host_managed_Cosmos_client()
    {
        var constructor = Assert.Single(typeof(DBUtil).GetConstructors());
        var parameterTypes = constructor
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.Contains(typeof(CosmosClient), parameterTypes);
    }
}
