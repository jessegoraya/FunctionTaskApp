using System.Net;
using System.Net.Http.Headers;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Taslow.Shared.Model;
using Taslow.Tenant.DAL.Interface;
using Taslow.Tenant.Model;
using Taslow.Tenant.Service.Interface;

namespace Taslow.Tenant.Service
{
    public class MicrosoftGraphMessageClient : IMicrosoftGraphMessageClient
    {
        private readonly ITenantRepository _tenantRepository;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public MicrosoftGraphMessageClient(
            ITenantRepository tenantRepository,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _tenantRepository = tenantRepository;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<TenantEmailExtractionQueueMessage> HydrateAsync(
            TenantEmailExtractionQueueMessage message,
            CancellationToken cancellationToken = default)
        {
            var (tenant, _) = await _tenantRepository.GetByIdAsync(message.TenantId, cancellationToken);
            var microsoftTenantId = tenant?.Identity.Microsoft?.MicrosoftTid?.Trim();
            if (string.IsNullOrWhiteSpace(microsoftTenantId))
            {
                throw new TenantEmailIngestionException(
                    "The tenant Microsoft directory binding is missing.",
                    isTransient: false);
            }

            var accessToken = await RequestAccessTokenAsync(microsoftTenantId, cancellationToken);
            var select = "id,internetMessageId,subject,body,bodyPreview,sentDateTime,from,toRecipients,ccRecipients,bccRecipients,conversationId";
            var messageUrl = $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(message.Mailbox)}/messages/{Uri.EscapeDataString(message.MessageId)}?$select={select}";
            using var request = new HttpRequestMessage(HttpMethod.Get, messageUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var client = _httpClientFactory.CreateClient(nameof(MicrosoftGraphMessageClient));
            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                throw new TenantEmailIngestionException(
                    "Microsoft Graph message hydration failed.",
                    isTransient: true,
                    ex);
            }

            using (response)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    throw new TenantEmailIngestionException(
                        $"Microsoft Graph message hydration failed with status {(int)response.StatusCode}.",
                        IsTransient(response.StatusCode));
                }

                var payload = JObject.Parse(content);
                message.InternetMessageId = payload.Value<string>("internetMessageId")?.Trim()
                    ?? message.MessageId;
                message.Subject = payload.Value<string>("subject") ?? string.Empty;
                message.BodyText = payload.SelectToken("body.content")?.Value<string>()
                    ?? payload.Value<string>("bodyPreview")
                    ?? string.Empty;
                message.SentDateTime = ReadUtcTimestamp(payload["sentDateTime"]);
                message.From = ReadParticipant(payload.SelectToken("from.emailAddress"));
                message.To = ReadParticipants(payload["toRecipients"]);
                message.Cc = ReadParticipants(payload["ccRecipients"]);
                message.Bcc = ReadParticipants(payload["bccRecipients"]);
                message.ConversationId = payload.Value<string>("conversationId");
            }

            if (string.IsNullOrWhiteSpace(message.Subject) && string.IsNullOrWhiteSpace(message.BodyText))
            {
                throw new TenantEmailIngestionException(
                    "Microsoft Graph returned no email subject or body.",
                    isTransient: false);
            }

            return message;
        }

        private async Task<string> RequestAccessTokenAsync(
            string microsoftTenantId,
            CancellationToken cancellationToken)
        {
            var clientId = _configuration["Auth:Microsoft:ClientId"];
            var clientSecret = _configuration["Auth:Microsoft:ClientSecret"];
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new TenantEmailIngestionException(
                    "Microsoft Graph application credentials are unavailable.",
                    isTransient: false);
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://login.microsoftonline.com/{Uri.EscapeDataString(microsoftTenantId)}/oauth2/v2.0/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["scope"] = "https://graph.microsoft.com/.default",
                    ["grant_type"] = "client_credentials"
                })
            };

            var client = _httpClientFactory.CreateClient(nameof(MicrosoftGraphMessageClient));
            using var response = await client.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new TenantEmailIngestionException(
                    $"Microsoft Graph token request failed with status {(int)response.StatusCode}.",
                    IsTransient(response.StatusCode));
            }

            var accessToken = JObject.Parse(content).Value<string>("access_token");
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new TenantEmailIngestionException(
                    "Microsoft Graph token response was invalid.",
                    isTransient: false);
            }

            return accessToken;
        }

        private static TenantEmailParticipant? ReadParticipant(JToken? token)
        {
            var email = token?.Value<string>("address")?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            return new TenantEmailParticipant
            {
                Email = email,
                Name = token?.Value<string>("name") ?? string.Empty
            };
        }

        private static string? ReadUtcTimestamp(JToken? token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }

            if (token.Type == JTokenType.Date)
            {
                return token.Value<DateTime>()
                    .ToUniversalTime()
                    .ToString("O", CultureInfo.InvariantCulture);
            }

            if (!DateTimeOffset.TryParse(
                    token.Value<string>(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var parsed))
            {
                throw new TenantEmailIngestionException(
                    "Microsoft Graph returned an invalid sentDateTime.",
                    isTransient: false);
            }

            return parsed.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        }

        private static List<TenantEmailParticipant> ReadParticipants(JToken? token)
        {
            return (token as JArray ?? new JArray())
                .Select(item => ReadParticipant(item["emailAddress"]))
                .Where(item => item != null)
                .Cast<TenantEmailParticipant>()
                .ToList();
        }

        private static bool IsTransient(HttpStatusCode statusCode)
        {
            return statusCode == HttpStatusCode.RequestTimeout
                || statusCode == (HttpStatusCode)429
                || (int)statusCode >= 500;
        }
    }
}
