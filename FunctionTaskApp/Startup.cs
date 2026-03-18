using System;
using System.Net.Http;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Taslow.Task.DAL;
using Taslow.Task.DAL.Interface;
using Taslow.Task.Client;
using Taslow.Task.Client.Interface;

[assembly: FunctionsStartup(typeof(FunctionTaskApp.Startup))]

namespace FunctionTaskApp
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            // Project Service Client (HTTP)
            builder.Services.AddHttpClient<IProjectServiceClient, ProjectServiceClient>(client =>
            {
                var baseUrl = Environment.GetEnvironmentVariable("ProjectServiceBaseUrl");

                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    throw new InvalidOperationException(
                        "ProjectServiceBaseUrl is not configured.");
                }

                client.BaseAddress = new Uri(baseUrl);
            });

            // Task DB Util
            builder.Services.AddScoped<ITaskDBUtil, DBUtil>();
        }
    }
}
