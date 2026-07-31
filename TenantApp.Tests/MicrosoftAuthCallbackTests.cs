using Taslow.Tenant.Function;
using Xunit;

namespace TenantApp.Tests
{
    public class MicrosoftAuthCallbackTests
    {
        [Fact]
        public void ParseMicrosoftCallback_ShouldRecognizeAdministratorConsent()
        {
            var callback = TenantAuthFunction.ParseMicrosoftCallback(new Uri(
                "https://api.example.test/auth/callback/microsoft?admin_consent=True&tenant=e736e61f-fb51-4307-84dd-0bcc9789d1fe&state=consent-state"));

            Assert.Equal(TenantAuthFunction.MicrosoftCallbackKind.AdminConsent, callback.Kind);
            Assert.Equal("e736e61f-fb51-4307-84dd-0bcc9789d1fe", callback.TenantId);
            Assert.Equal("consent-state", callback.State);
            Assert.Null(callback.Code);
        }

        [Fact]
        public void ParseMicrosoftCallback_ShouldRecognizeAuthorizationCodeLogin()
        {
            var callback = TenantAuthFunction.ParseMicrosoftCallback(new Uri(
                "https://api.example.test/auth/callback/microsoft?code=authorization-code&state=signed-login-state"));

            Assert.Equal(TenantAuthFunction.MicrosoftCallbackKind.AuthorizationCode, callback.Kind);
            Assert.Equal("authorization-code", callback.Code);
            Assert.Equal("signed-login-state", callback.State);
        }

        [Fact]
        public void ParseMicrosoftCallbackForm_ShouldRecognizeLargeAuthorizationCodeLogin()
        {
            var authorizationCode = new string('a', 4_096);
            var callback = TenantAuthFunction.ParseMicrosoftCallbackForm(
                $"code={authorizationCode}&state=signed-login-state");

            Assert.Equal(TenantAuthFunction.MicrosoftCallbackKind.AuthorizationCode, callback.Kind);
            Assert.Equal(authorizationCode, callback.Code);
            Assert.Equal("signed-login-state", callback.State);
        }

        [Fact]
        public void ParseMicrosoftCallbackForm_ShouldDecodeProviderErrors()
        {
            var callback = TenantAuthFunction.ParseMicrosoftCallbackForm(
                "error=access_denied&error_description=Administrator+declined");

            Assert.Equal(TenantAuthFunction.MicrosoftCallbackKind.ProviderError, callback.Kind);
            Assert.Equal("Administrator declined", callback.ErrorDescription);
        }

        [Fact]
        public void ParseMicrosoftCallback_ShouldPreserveProviderErrorDescription()
        {
            var callback = TenantAuthFunction.ParseMicrosoftCallback(new Uri(
                "https://api.example.test/auth/callback/microsoft?error=access_denied&error_description=Administrator+declined"));

            Assert.Equal(TenantAuthFunction.MicrosoftCallbackKind.ProviderError, callback.Kind);
            Assert.Equal("Administrator declined", callback.ErrorDescription);
        }

        [Fact]
        public void ParseMicrosoftCallback_ShouldRejectAdministratorConsentWithoutTenant()
        {
            var callback = TenantAuthFunction.ParseMicrosoftCallback(new Uri(
                "https://api.example.test/auth/callback/microsoft?admin_consent=True&state=consent-state"));

            Assert.Equal(TenantAuthFunction.MicrosoftCallbackKind.Invalid, callback.Kind);
            Assert.Equal(
                "Microsoft administrator consent callback did not include a valid tenant ID.",
                callback.ErrorDescription);
        }

        [Fact]
        public void ParseMicrosoftCallback_ShouldRejectAdministratorConsentWithoutState()
        {
            var callback = TenantAuthFunction.ParseMicrosoftCallback(new Uri(
                "https://api.example.test/auth/callback/microsoft?admin_consent=True&tenant=e736e61f-fb51-4307-84dd-0bcc9789d1fe"));

            Assert.Equal(TenantAuthFunction.MicrosoftCallbackKind.Invalid, callback.Kind);
            Assert.Equal(
                "Microsoft administrator consent callback did not include state.",
                callback.ErrorDescription);
        }

        [Fact]
        public void ParseMicrosoftCallback_ShouldRejectUnknownCallback()
        {
            var callback = TenantAuthFunction.ParseMicrosoftCallback(new Uri(
                "https://api.example.test/auth/callback/microsoft?state=state-only"));

            Assert.Equal(TenantAuthFunction.MicrosoftCallbackKind.Invalid, callback.Kind);
        }
    }
}
