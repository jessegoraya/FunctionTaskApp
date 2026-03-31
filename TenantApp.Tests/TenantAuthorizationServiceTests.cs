using System.Net;
using System;
using System.Collections.Generic;
using Taslow.Shared.Model;
using Taslow.Tenant.Model;
using Taslow.Tenant.Service;
using Xunit;

namespace TenantApp.Tests
{
    public class TenantAuthorizationServiceTests
    {
        private readonly TenantAuthorizationService _service = new();

        [Fact]
        public void ResolveAuthContext_ShouldFail_WhenHeadersDisabled()
        {
            var headers = new Dictionary<string, string>
            {
                ["x-taslow-dev-role"] = TenantRoles.TaslowAdmin
            };

            var ex = Assert.Throws<TenantApiException>((Action)(() => _service.ResolveAuthContext(headers, allowDevHeaders: false)));
            Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        }

        [Fact]
        public void ResolveAuthContext_ShouldRequireTenantHeader_ForTenantAdmin()
        {
            var headers = new Dictionary<string, string>
            {
                ["x-taslow-dev-role"] = TenantRoles.TenantAdmin
            };

            var ex = Assert.Throws<TenantApiException>((Action)(() => _service.ResolveAuthContext(headers, allowDevHeaders: true)));
            Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        }

        [Fact]
        public void EnsureCanReadOrUpdateTenant_ShouldDenyCrossTenantAccess()
        {
            var auth = new TenantAuthContext
            {
                Role = TenantRoles.TenantAdmin,
                TenantId = "tenant-a"
            };

            var ex = Assert.Throws<TenantApiException>(() => _service.EnsureCanReadOrUpdateTenant(auth, "tenant-b"));
            Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        }
    }
}
