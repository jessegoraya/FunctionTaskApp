using System.Security.Cryptography;
using System.Text;

namespace Taslow.Tenant.Model
{
    public static class TenantEmailIdempotencyKeyBuilder
    {
        public static string Build(string tenantId, string mailbox, string internetMessageId, string direction)
        {
            var raw = string.Join(
                "|",
                tenantId.Trim().ToLowerInvariant(),
                mailbox.Trim().ToLowerInvariant(),
                internetMessageId.Trim().ToLowerInvariant(),
                direction.Trim().ToLowerInvariant());

            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
