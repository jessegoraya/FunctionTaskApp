using Azure.Core;
using Azure.Identity;
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
        services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential(
            new DefaultAzureCredentialOptions
            {
                ExcludeInteractiveBrowserCredential = true
            }));
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
        services.AddSingleton<IGraphNotificationValidator, GraphNotificationValidator>();
        services.AddSingleton<IMicrosoftGraphMessageClient, MicrosoftGraphMessageClient>();
        services.AddSingleton<IEmailExtractionClient, FoundryEmailExtractionClient>();
        services.AddSingleton<IEmailTaskWriteClient, LogicAppEmailTaskWriteClient>();
        services.AddSingleton<ITenantEmailIngestionService, TenantEmailIngestionService>();
    })
    .Build();

host.Run();
