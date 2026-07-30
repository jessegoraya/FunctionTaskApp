using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Taslow.Shared.Infrastructure;
using Taslow.Task.DAL.Interface;

namespace Taslow.Task.DAL;

public static class TaskDataAccessRegistration
{
    public static IServiceCollection AddTaskDataAccess(this IServiceCollection services)
    {
        services.AddSingleton<CosmosClient>(_ =>
            CosmosClientFactory.Create(Environment.GetEnvironmentVariable));
        services.AddScoped<ITaskDBUtil, DBUtil>();
        return services;
    }
}
