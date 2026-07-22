using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Taslow.Shared.Model;
using Taslow.Tenant.DAL.Interface;
using Taslow.Tenant.Model;
using Taslow.Tenant.Service.Interface;

namespace Taslow.Tenant.Service
{
    public class GraphNotificationValidator : IGraphNotificationValidator
    {
        private const int DigestLength = 16;
        private readonly ITenantRepository _tenantRepository;
        private readonly byte[] _signingKey;

        public GraphNotificationValidator(
            ITenantRepository tenantRepository,
            IConfiguration configuration)
        {
            _tenantRepository = tenantRepository;
            var signingKey = configuration["TenantEmailIngestion:ClientStateSigningKey"]
                ?? configuration["TenantEmailIngestion__ClientStateSigningKey"];
            if (string.IsNullOrWhiteSpace(signingKey))
            {
                throw new InvalidOperationException(
                    "TenantEmailIngestion ClientStateSigningKey is required.");
            }

            _signingKey = Encoding.UTF8.GetBytes(signingKey);
        }

        public async Task<GraphNotificationRoute?> ValidateAsync(
            string clientState,
            string subscriptionId,
            CancellationToken cancellationToken = default)
        {
            if (!TryReadToken(clientState, out var tenantId, out var mailboxHash, out var direction))
            {
                return null;
            }

            var (tenant, _) = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
            if (tenant == null
                || !TenantStatuses.Active.Equals(tenant.Tenant.Status, StringComparison.OrdinalIgnoreCase)
                || tenant.EmailIntegration.Graph?.Enabled != true
                || tenant.EmailIntegration.Graph.EmailIngestionEnabled != true)
            {
                return null;
            }

            var mailboxMatches = (tenant.EmailIntegration.MailboxStates ?? new List<TenantMailboxStateDTO>())
                .Where(item => item.Status.Equals("active", StringComparison.OrdinalIgnoreCase))
                .Where(item => FixedEquals(CreateMailboxDigest(item.MailboxKey), mailboxHash))
                .Select(item => item.MailboxKey.Trim().ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (mailboxMatches.Count != 1)
            {
                return null;
            }

            var mailbox = mailboxMatches[0];
            var subscriptionMatches = (tenant.EmailIntegration.SubscriptionRegistry
                    ?? new List<TenantSubscriptionRegistryItemDTO>())
                .Where(item => item.Provider.Equals(IntegrationProviders.Graph, StringComparison.OrdinalIgnoreCase))
                .Where(item => item.Status.Equals("active", StringComparison.OrdinalIgnoreCase))
                .Where(item => item.SubscriptionId.Equals(subscriptionId, StringComparison.Ordinal))
                .Where(item => string.IsNullOrWhiteSpace(item.MailboxKey)
                    || item.MailboxKey.Equals(mailbox, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (subscriptionMatches.Count != 1)
            {
                return null;
            }

            return new GraphNotificationRoute
            {
                TenantId = tenantId,
                Mailbox = mailbox,
                Direction = direction
            };
        }

        public static string CreateToken(
            string tenantId,
            string mailbox,
            string direction,
            string signingKey)
        {
            if (!Guid.TryParse(tenantId, out var parsedTenantId))
            {
                throw new ArgumentException("tenantId must be a GUID.", nameof(tenantId));
            }

            if (string.IsNullOrWhiteSpace(mailbox))
            {
                throw new ArgumentException("mailbox is required.", nameof(mailbox));
            }

            if (string.IsNullOrWhiteSpace(signingKey))
            {
                throw new ArgumentException("signingKey is required.", nameof(signingKey));
            }

            var directionCode = direction.Equals(TenantEmailDirections.Sent, StringComparison.OrdinalIgnoreCase)
                ? "s"
                : throw new ArgumentException("Only sent-mail subscriptions are supported.", nameof(direction));
            var mailboxDigest = ToBase64Url(CreateMailboxDigest(mailbox));
            var payload = $"v1.{parsedTenantId:N}.{mailboxDigest}.{directionCode}";
            var signature = ComputeSignature(payload, Encoding.UTF8.GetBytes(signingKey));
            return $"{payload}.{ToBase64Url(signature)}";
        }

        private bool TryReadToken(
            string clientState,
            out string tenantId,
            out byte[] mailboxHash,
            out string direction)
        {
            tenantId = string.Empty;
            mailboxHash = Array.Empty<byte>();
            direction = string.Empty;
            var parts = (clientState ?? string.Empty).Split('.', StringSplitOptions.None);
            if (parts.Length != 5
                || !parts[0].Equals("v1", StringComparison.Ordinal)
                || !Guid.TryParseExact(parts[1], "N", out var parsedTenantId)
                || !TryFromBase64Url(parts[2], out mailboxHash)
                || mailboxHash.Length != DigestLength
                || !TryFromBase64Url(parts[4], out var suppliedSignature)
                || suppliedSignature.Length != DigestLength)
            {
                return false;
            }

            direction = parts[3] switch
            {
                "s" => TenantEmailDirections.Sent,
                _ => string.Empty
            };
            if (string.IsNullOrWhiteSpace(direction))
            {
                return false;
            }

            var payload = string.Join('.', parts.Take(4));
            var expectedSignature = ComputeSignature(payload, _signingKey);
            if (!FixedEquals(expectedSignature, suppliedSignature))
            {
                return false;
            }

            tenantId = parsedTenantId.ToString();
            return true;
        }

        private static byte[] CreateMailboxDigest(string mailbox)
        {
            var normalized = (mailbox ?? string.Empty).Trim().ToLowerInvariant();
            var digest = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
            return digest.Take(DigestLength).ToArray();
        }

        private static byte[] ComputeSignature(string payload, byte[] key)
        {
            var digest = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(payload));
            return digest.Take(DigestLength).ToArray();
        }

        private static bool FixedEquals(byte[] left, byte[] right)
        {
            return left.Length == right.Length
                && CryptographicOperations.FixedTimeEquals(left, right);
        }

        private static string ToBase64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static bool TryFromBase64Url(string value, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();
            try
            {
                var padded = value.Replace('-', '+').Replace('_', '/');
                padded = padded.PadRight(padded.Length + ((4 - padded.Length % 4) % 4), '=');
                bytes = Convert.FromBase64String(padded);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
