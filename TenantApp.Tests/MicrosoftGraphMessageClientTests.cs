using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Taslow.Shared.Model;
using Taslow.Tenant.Service;
using Xunit;

namespace TenantApp.Tests
{
    public class MicrosoftGraphMessageClientTests
    {
        [Fact]
        public async Task HydrateAsync_ShouldUseCustomerTenantAndReturnAuthoritativeMessage()
        {
            var handler = new RecordingHandler();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:Microsoft:ClientId"] = "client-id",
                    ["Auth:Microsoft:ClientSecret"] = "client-secret"
                })
                .Build();
            var tenant = CreateTenant();
            var client = new MicrosoftGraphMessageClient(
                new FakeTenantRepository(tenant),
                new SingleClientFactory(new HttpClient(handler)),
                configuration);

            var hydrated = await client.HydrateAsync(new TenantEmailExtractionQueueMessage
            {
                TenantId = tenant.Id,
                Mailbox = "ahassan@bloomsky.onmicrosoft.com",
                MessageId = "AAMk-message"
            });

            Assert.Contains("customer-directory-id/oauth2/v2.0/token", handler.TokenUrl);
            Assert.Equal("Bearer graph-token", handler.MessageAuthorization);
            Assert.Equal("<internet-message@bloomsky.onmicrosoft.com>", hydrated.InternetMessageId);
            Assert.Equal("Complete readiness review", hydrated.Subject);
            Assert.Equal("Please complete the readiness review.", hydrated.BodyText);
            Assert.Equal("2026-07-22T12:00:00.0000000Z", hydrated.SentDateTime);
            Assert.Equal("sender@bloomsky.onmicrosoft.com", hydrated.From!.Email);
            Assert.Single(hydrated.To);
        }

        private static TenantDocumentDTO CreateTenant()
        {
            return new TenantDocumentDTO
            {
                Id = "11111111-1111-4111-8111-111111111111",
                Identity = new TenantIdentityPatchRequest
                {
                    Microsoft = new TenantMicrosoftIdentityDTO
                    {
                        MicrosoftTid = "customer-directory-id"
                    }
                }
            };
        }

        private sealed class RecordingHandler : HttpMessageHandler
        {
            public string TokenUrl { get; private set; } = string.Empty;
            public string? MessageAuthorization { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                if (request.RequestUri!.Host.Equals(
                    "login.microsoftonline.com",
                    StringComparison.OrdinalIgnoreCase))
                {
                    TokenUrl = request.RequestUri.AbsoluteUri;
                    return Task.FromResult(Json(HttpStatusCode.OK, new
                    {
                        access_token = "graph-token"
                    }));
                }

                MessageAuthorization = request.Headers.Authorization?.ToString();
                return Task.FromResult(Json(HttpStatusCode.OK, new
                {
                    id = "AAMk-message",
                    internetMessageId = "<internet-message@bloomsky.onmicrosoft.com>",
                    subject = "Complete readiness review",
                    body = new
                    {
                        contentType = "text",
                        content = "Please complete the readiness review."
                    },
                    sentDateTime = "2026-07-22T12:00:00Z",
                    from = new
                    {
                        emailAddress = new
                        {
                            address = "sender@bloomsky.onmicrosoft.com",
                            name = "Sender"
                        }
                    },
                    toRecipients = new[]
                    {
                        new
                        {
                            emailAddress = new
                            {
                                address = "ahassan@bloomsky.onmicrosoft.com",
                                name = "Amina Hassan"
                            }
                        }
                    },
                    ccRecipients = Array.Empty<object>(),
                    bccRecipients = Array.Empty<object>(),
                    conversationId = "conversation-1"
                }));
            }

            private static HttpResponseMessage Json(HttpStatusCode status, object payload)
            {
                return new HttpResponseMessage(status)
                {
                    Content = new StringContent(
                        Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                        Encoding.UTF8,
                        "application/json")
                };
            }
        }

        private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => client;
        }
    }
}
