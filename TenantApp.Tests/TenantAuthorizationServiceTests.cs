using System.Net;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Taslow.Shared.Model;
using Taslow.Tenant.Model;
using Taslow.Tenant.Service;
using Xunit;

namespace TenantApp.Tests
{
    public class TenantAuthorizationServiceTests
    {
        private readonly TaslowJwtService _jwtService;
        private readonly TenantAuthorizationService _service;

        public TenantAuthorizationServiceTests()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:CookieName"] = "taslow_auth",
                    ["Auth:Environment"] = TaslowEnvironments.Test,
                    ["Auth:JwtSigningKey"] = "taslow-test-authorization-service-signing-key-for-tests"
                })
                .Build();
            _jwtService = new TaslowJwtService(configuration);
            _service = new TenantAuthorizationService(_jwtService, configuration);
        }

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
        public void ResolveAuthContext_ShouldResolveSignedCookie_WhenDevHeadersAreDisabled()
        {
            var token = _jwtService.IssueToken(new TenantAuthContext
            {
                Role = TenantRoles.TenantAdmin,
                TenantId = "tenant-a",
                Subject = "microsoft:tenant-a:admin",
                Provider = AuthProviders.Microsoft,
                IdentityMode = IdentityModes.Integrated,
                Environment = TaslowEnvironments.Test,
                Roles = new List<string> { TenantRoles.TenantAdmin }
            }, DateTimeOffset.UtcNow.AddMinutes(5));

            var auth = _service.ResolveAuthContext(new Dictionary<string, string>
            {
                ["Cookie"] = $"other=value; taslow_auth={token}"
            }, allowDevHeaders: false);

            Assert.Equal("tenant-a", auth.TenantId);
            Assert.Equal(TenantRoles.TenantAdmin, auth.Role);
            Assert.Equal("microsoft:tenant-a:admin", auth.Subject);
        }

        [Fact]
        public void ResolveAuthContext_ShouldRejectInvalidSignedCookie()
        {
            var headers = new Dictionary<string, string>
            {
                ["Cookie"] = "taslow_auth=not-a-valid-token"
            };

            var ex = Assert.Throws<TenantApiException>(() =>
                _service.ResolveAuthContext(headers, allowDevHeaders: false));

            Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
            Assert.Equal(AuthErrorCodes.TokenInvalid, ex.Code);
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
