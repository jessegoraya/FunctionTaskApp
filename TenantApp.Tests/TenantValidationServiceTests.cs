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

        [Fact]
        public void ValidateTenantUsersPatch_ShouldRejectTaslowAdminAssignment()
        {
            var request = new TenantUsersPatchRequest
            {
                Users = new List<TenantUserRoleAssignmentRequest>
                {
                    new()
                    {
                        UserId = "owner-1",
                        DisplayName = "Owner One",
                        Email = "owner@example.com",
                        Roles = new List<string> { TenantRoles.TaslowAdmin }
                    }
                }
            };

            var ex = Assert.Throws<TenantApiException>(() => _service.ValidateTenantUsersPatch(request));
            Assert.Equal(HttpStatusCode.UnprocessableEntity, ex.StatusCode);
            Assert.Contains("cannot be assigned", ex.Message);
        }

        [Fact]
        public void ValidateTenantUsersPatch_ShouldRejectDuplicateEmails()
        {
            var request = new TenantUsersPatchRequest
            {
                Users = new List<TenantUserRoleAssignmentRequest>
                {
                    new() { UserId = "user-1", DisplayName = "User One", Email = "same@example.com", Roles = new List<string> { TenantRoles.TenantUser } },
                    new() { UserId = "user-2", DisplayName = "User Two", Email = "SAME@example.com", Roles = new List<string> { TenantRoles.TenantUser } }
                }
            };

            var ex = Assert.Throws<TenantApiException>(() => _service.ValidateTenantUsersPatch(request));
            Assert.Equal(HttpStatusCode.UnprocessableEntity, ex.StatusCode);
            Assert.Contains("duplicate email", ex.Message);
        }
    }
}
