using System.Net;
using Taslow.Shared.Model;
using Taslow.Tenant.Model;
using Taslow.Tenant.Service;
using Xunit;

namespace TenantApp.Tests
{
    public class TenantValidationServiceTests
    {
        private readonly TenantValidationService _service = new();

        [Fact]
        public void ValidateIfMatch_ShouldFail_WhenMissing()
        {
            var ex = Assert.Throws<TenantApiException>(() => _service.ValidateIfMatch(null));
            Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
            Assert.Equal(TenantErrorCodes.MissingIfMatch, ex.Code);
        }

        [Fact]
        public void ValidateIdentityPatch_ShouldRejectProviderMismatch()
        {
            var current = new TenantDocumentDTO
            {
                Administration = new TenantAdministrationPatchRequest
                {
                    Provider = TenantProviders.Microsoft
                }
            };

            var request = new TenantIdentityPatchRequest
            {
                Google = new TenantGoogleIdentityDTO
                {
                    HostedDomainHd = "acme.example"
                }
            };

            var ex = Assert.Throws<TenantApiException>(() => _service.ValidateIdentityPatch(request, current));
            Assert.Equal(HttpStatusCode.UnprocessableEntity, ex.StatusCode);
            Assert.Equal(TenantErrorCodes.ProviderMismatch, ex.Code);
        }

        [Fact]
        public void ValidateBillingPatch_ShouldRejectInvalidCurrency()
        {
            var request = new TenantBillingPatchRequest
            {
                Currency = "US"
            };

            var ex = Assert.Throws<TenantApiException>(() => _service.ValidateBillingPatch(request));
            Assert.Equal(HttpStatusCode.UnprocessableEntity, ex.StatusCode);
        }

        [Fact]
        public void ValidateCreateRequest_ShouldRequireContactabilityFields()
        {
            var request = new TenantCreateRequest
            {
                DisplayName = "Acme"
            };

            var ex = Assert.Throws<TenantApiException>(() => _service.ValidateCreateRequest(request));
            Assert.Equal(HttpStatusCode.UnprocessableEntity, ex.StatusCode);
            Assert.Equal(TenantErrorCodes.ValidationFailed, ex.Code);
        }

        [Fact]
        public void ValidateDetailsPatch_ShouldRejectInvalidCountryCode()
        {
            var request = new TenantDetailsPatchRequest
            {
                MailingCountryCode = "usa"
            };

            var ex = Assert.Throws<TenantApiException>(() => _service.ValidateDetailsPatch(request));
            Assert.Equal(HttpStatusCode.UnprocessableEntity, ex.StatusCode);
            Assert.Equal(TenantErrorCodes.ValidationFailed, ex.Code);
        }

        [Fact]
        public void ValidateDetailsPatch_ShouldRejectInvalidCompanyPocPhone()
        {
            var request = new TenantDetailsPatchRequest
            {
                CompanyPocPhone = "abc"
            };

            var ex = Assert.Throws<TenantApiException>(() => _service.ValidateDetailsPatch(request));
            Assert.Equal(HttpStatusCode.UnprocessableEntity, ex.StatusCode);
            Assert.Equal(TenantErrorCodes.ValidationFailed, ex.Code);
        }
    }
}
