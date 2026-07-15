using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Taslow.Project.DAL;
using Taslow.Project.DAL.Interface;
using Taslow.Project.Service;
using Taslow.Project.Service.Interface;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddSingleton<IProjectDBUtil, DBUtil>();
        services.AddSingleton<IProjectRequestValidator, ProjectRequestValidator>();
        services.AddSingleton<IProjectService, ProjectService>();
    })
    .Build();

host.Run();
