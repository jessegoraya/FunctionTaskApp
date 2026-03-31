using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Taslow.Tenant.DAL;
using Taslow.Tenant.DAL.Interface;
using Taslow.Tenant.Service;
using Taslow.Tenant.Service.Interface;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddSingleton<ITenantRepository, TenantRepository>();
        services.AddSingleton<ITenantValidationService, TenantValidationService>();
        services.AddSingleton<ITenantAuthorizationService, TenantAuthorizationService>();
        services.AddSingleton<ITenantService, TenantService>();
    })
    .Build();

host.Run();
