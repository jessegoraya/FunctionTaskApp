using Azure.Identity;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Taslow.Shared.Model;
using Taslow.Tenant.Service.Interface;

namespace Taslow.Tenant.Service
{
    public class TenantEmailQueueClient : ITenantEmailQueueClient
    {
        private readonly QueueClient _queueClient;

        public TenantEmailQueueClient(IConfiguration configuration)
        {
            var queueName = configuration["TenantEmailIngestionQueueName"] ?? "tenant-email-extraction";
            var options = new QueueClientOptions
            {
                MessageEncoding = QueueMessageEncoding.Base64
            };
            var connection = configuration["AzureWebJobsStorage"];

            if (!string.IsNullOrWhiteSpace(connection))
            {
                _queueClient = new QueueClient(connection, queueName, options);
                return;
            }

            var queueServiceUri = ResolveQueueServiceUri(configuration);

            if (string.IsNullOrWhiteSpace(queueServiceUri))
            {
                throw new InvalidOperationException(
                    "Configure AzureWebJobsStorage for local development or AzureWebJobsStorage__accountName for managed identity.");
            }

            _queueClient = new QueueClient(
                new Uri($"{queueServiceUri.TrimEnd('/')}/{queueName}"),
                new DefaultAzureCredential(),
                options);
        }

        internal static string? ResolveQueueServiceUri(IConfiguration configuration)
        {
            // Environment variables use "__", but IConfiguration exposes the
            // normalized ":" path inside the isolated worker.
            var queueServiceUri = configuration["AzureWebJobsStorage:queueServiceUri"]
                ?? configuration["AzureWebJobsStorage__queueServiceUri"];
            if (!string.IsNullOrWhiteSpace(queueServiceUri))
            {
                return queueServiceUri;
            }

            var accountName = configuration["AzureWebJobsStorage:accountName"]
                ?? configuration["AzureWebJobsStorage__accountName"];
            return string.IsNullOrWhiteSpace(accountName)
                ? null
                : $"https://{accountName}.queue.core.windows.net";
        }

        public async Task EnqueueAsync(TenantEmailExtractionQueueMessage message, CancellationToken cancellationToken = default)
        {
            await _queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            var payload = JsonConvert.SerializeObject(message);
            _ = await _queueClient.SendMessageAsync(payload, cancellationToken: cancellationToken);
        }
    }
}
