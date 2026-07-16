using Taslow.Tenant.Function;
using Xunit;

namespace TenantApp.Tests
{
    public class TenantAuthCookieTests
    {
        [Fact]
        public void BuildAuthCookieHeader_ShouldAllowCrossSiteSessionForSecureHosts()
        {
            var header = TenantAuthFunction.BuildAuthCookieHeader(
                "taslow_auth",
                "session-token",
                28800,
                secure: true);

            Assert.Equal(
                "taslow_auth=session-token; HttpOnly; SameSite=None; Path=/; Max-Age=28800; Secure",
                header);
        }

        [Fact]
        public void BuildAuthCookieHeader_ShouldSupportLocalHttpDevelopment()
        {
            var header = TenantAuthFunction.BuildAuthCookieHeader(
                "taslow_auth",
                "session-token",
                28800,
                secure: false);

            Assert.Equal(
                "taslow_auth=session-token; HttpOnly; SameSite=Lax; Path=/; Max-Age=28800",
                header);
        }

        [Fact]
        public void BuildAuthCookieHeader_ShouldClearCookieWithMatchingSecurityAttributes()
        {
            var header = TenantAuthFunction.BuildAuthCookieHeader(
                "taslow_auth",
                string.Empty,
                0,
                secure: true);

            Assert.Equal(
                "taslow_auth=; HttpOnly; SameSite=None; Path=/; Max-Age=0; Secure",
                header);
        }
    }
}
