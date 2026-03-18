using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Taslow.Project.DAL;
using Taslow.Project.DAL.Interface;
using Taslow.Project.Service;
using Taslow.Project.Service.Interface;

[assembly: FunctionsStartup(typeof(ProjectApp.Startup))]

namespace ProjectApp
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddScoped<IProjectDBUtil, DBUtil>();
            builder.Services.AddHttpClient<IProjectScopeSyncPublisher, ProjectScopeSyncPublisher>();
        }
    }
}
