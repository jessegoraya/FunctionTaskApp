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
            builder.Services.AddSingleton<IProjectServiceClient>(sp =>
            {
                var baseUrl = Environment.GetEnvironmentVariable("ProjectServiceBaseUrl");

                if (string.IsNullOrEmpty(baseUrl))
                    throw new InvalidOperationException("ProjectServiceBaseUrl is not configured.");

                return new ProjectServiceClient(
                    new HttpClient
                    {
                        BaseAddress = new Uri(baseUrl)
                    });
            });

            // Task DB Util
            builder.Services.AddScoped<ITaskDBUtil, DBUtil>();
        }
    }
}
