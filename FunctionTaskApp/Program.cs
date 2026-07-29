using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Taslow.Task.Client;
using Taslow.Task.Client.Interface;
using Taslow.Task.DAL;
using Taslow.Task.DAL.Interface;
using Taslow.Task.Service;
using Taslow.Task.Service.Interface;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddHttpClient<IProjectServiceClient, ProjectServiceClient>(client =>
        {
            var baseUrl = Environment.GetEnvironmentVariable("ProjectServiceBaseUrl");

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new InvalidOperationException(
                    "ProjectServiceBaseUrl is not configured.");
            }

            client.BaseAddress = new Uri($"{baseUrl.TrimEnd('/')}/");
        });

        services.AddScoped<ITaskDBUtil, DBUtil>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<ITaskAuthorizationService, TaskAuthorizationService>();
    })
    .Build();

host.Run();
