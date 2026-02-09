using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Taslow.Project.DAL;
using Taslow.Project.DAL.Interface;

[assembly: FunctionsStartup(typeof(ProjectApp.Startup))]

namespace ProjectApp
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            // Project DB Util
            builder.Services.AddScoped<IProjectDBUtil, DBUtil>();
        }
    }
}

