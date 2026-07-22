using Microsoft.Extensions.Configuration;
using Taslow.Shared.Model;
using Taslow.Tenant.Model;
using Taslow.Tenant.Service;
using Xunit;

namespace TenantApp.Tests
{
    public class GraphNotificationValidatorTests
    {
        private const string SigningKey = "test-client-state-signing-key";
        private const string TenantId = "11111111-1111-4111-8111-111111111111";
        private const string Mailbox = "ahassan@bloomsky.onmicrosoft.com";
        private const string SubscriptionId = "graph-subscription-1";

        [Fact]
        public async Task ValidateAsync_ShouldResolveSignedAllowListedSubscription()
        {
            var validator = CreateValidator();
            var token = GraphNotificationValidator.CreateToken(
                TenantId,
                Mailbox,
                TenantEmailDirections.Sent,
                SigningKey);

            var route = await validator.ValidateAsync(token, SubscriptionId);

            Assert.NotNull(route);
            Assert.Equal(TenantId, route!.TenantId);
            Assert.Equal(Mailbox, route.Mailbox);
            Assert.Equal(TenantEmailDirections.Sent, route.Direction);
            Assert.True(token.Length < 128);
        }

        [Fact]
        public async Task ValidateAsync_ShouldRejectTamperedTokenAndWrongSubscription()
        {
            var validator = CreateValidator();
            var token = GraphNotificationValidator.CreateToken(
                TenantId,
                Mailbox,
                TenantEmailDirections.Sent,
                SigningKey);
            var tampered = token[..^1] + (token[^1] == 'a' ? 'b' : 'a');

            Assert.Null(await validator.ValidateAsync(tampered, SubscriptionId));
            Assert.Null(await validator.ValidateAsync(token, "different-subscription"));
        }

        [Fact]
        public async Task ValidateAsync_ShouldRejectMailboxOutsideTenantAllowList()
        {
            var validator = CreateValidator();
            var token = GraphNotificationValidator.CreateToken(
                TenantId,
                "other@bloomsky.onmicrosoft.com",
                TenantEmailDirections.Sent,
                SigningKey);

            Assert.Null(await validator.ValidateAsync(token, SubscriptionId));
        }

        [Fact]
        public void CreateToken_ShouldRejectBlankMailbox()
        {
            var action = () => GraphNotificationValidator.CreateToken(
                TenantId,
                " ",
                TenantEmailDirections.Sent,
                SigningKey);

            var exception = Assert.Throws<ArgumentException>(action);
            Assert.Equal("mailbox", exception.ParamName);
        }

        [Fact]
        public void CreateToken_ShouldRejectBlankSigningKey()
        {
            var action = () => GraphNotificationValidator.CreateToken(
                TenantId,
                Mailbox,
                TenantEmailDirections.Sent,
                " ");

            var exception = Assert.Throws<ArgumentException>(action);
            Assert.Equal("signingKey", exception.ParamName);
        }

        private static GraphNotificationValidator CreateValidator()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TenantEmailIngestion:ClientStateSigningKey"] = SigningKey
                })
                .Build();
            var tenant = new TenantDocumentDTO
            {
                Id = TenantId,
                Tenant = new TenantCoreDTO
                {
                    TenantId = TenantId,
                    Status = TenantStatuses.Active
                },
                EmailIntegration = new TenantEmailIntegrationPatchRequest
                {
                    Graph = new TenantGraphIntegrationDTO
                    {
                        Enabled = true,
                        EmailIngestionEnabled = true
                    },
                    MailboxStates = new List<TenantMailboxStateDTO>
                    {
                        new() { MailboxKey = Mailbox, Status = "active" }
                    },
                    SubscriptionRegistry = new List<TenantSubscriptionRegistryItemDTO>
                    {
                        new()
                        {
                            SubscriptionId = SubscriptionId,
                            MailboxKey = Mailbox,
                            Provider = IntegrationProviders.Graph,
                            Status = "active"
                        }
                    }
                }
            };
            return new GraphNotificationValidator(
                new FakeTenantRepository(tenant),
                configuration);
        }
    }
}
