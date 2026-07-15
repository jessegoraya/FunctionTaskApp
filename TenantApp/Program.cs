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
        services.AddHttpClient();
        services.AddSingleton<ITenantRepository, TenantRepository>();
        services.AddSingleton<IAuthenticationAuditRepository, AuthenticationAuditRepository>();
        services.AddSingleton<ITenantEmailIngestionStateRepository, TenantEmailIngestionStateRepository>();
        services.AddSingleton<ITenantValidationService, TenantValidationService>();
        services.AddSingleton<ITenantAuthorizationService, TenantAuthorizationService>();
        services.AddSingleton<ITaslowJwtService, TaslowJwtService>();
        services.AddSingleton<ITenantUserCatalogService, TenantUserCatalogService>();
        services.AddSingleton<ITenantAuthService, TenantAuthService>();
        services.AddSingleton<ITenantService, TenantService>();
        services.AddSingleton<ITenantEmailQueueClient, TenantEmailQueueClient>();
        services.AddSingleton<IEmailExtractionClient, PromptflowEmailExtractionClient>();
        services.AddSingleton<ITenantEmailIngestionService, TenantEmailIngestionService>();
    })
    .Build();

host.Run();
