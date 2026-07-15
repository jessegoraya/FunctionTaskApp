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
            var connection = configuration["AzureWebJobsStorage"];
            var queueName = configuration["TenantEmailIngestionQueueName"] ?? "tenant-email-extraction";

            if (string.IsNullOrWhiteSpace(connection))
            {
                throw new InvalidOperationException("AzureWebJobsStorage setting is missing.");
            }

            _queueClient = new QueueClient(
                connection,
                queueName,
                new QueueClientOptions
                {
                    MessageEncoding = QueueMessageEncoding.Base64
                });
        }

        public async Task EnqueueAsync(TenantEmailExtractionQueueMessage message, CancellationToken cancellationToken = default)
        {
            await _queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            var payload = JsonConvert.SerializeObject(message);
            _ = await _queueClient.SendMessageAsync(payload, cancellationToken: cancellationToken);
        }
    }
}
