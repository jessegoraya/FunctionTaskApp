using System.Net;
using System.Net.Mail;
using Taslow.Shared.Model;
using Taslow.Tenant.Model;
using Taslow.Tenant.Service.Interface;

namespace Taslow.Tenant.Service
{
    public class TenantValidationService : ITenantValidationService
    {
        public void ValidateCreateRequest(TenantCreateRequest request)
        {
            if (request == null)
            {
                throw Validation("Create request is required.");
            }

            if (string.IsNullOrWhiteSpace(request.DisplayName))
            {
                throw Validation("display_name is required.");
            }

            if (request.DisplayName.Length > 256)
            {
                throw Validation("display_name cannot exceed 256 characters.");
            }

            if (!string.IsNullOrWhiteSpace(request.Status)
                && !TenantStatuses.All.Contains(request.Status))
            {
                throw Validation("status is invalid.");
            }

            if (!string.IsNullOrWhiteSpace(request.Provider)
                && !TenantProviders.All.Contains(request.Provider))
            {
                throw Validation("provider is invalid.");
            }
        }

        public void ValidateDetailsPatch(TenantDetailsPatchRequest request)
        {
            if (request == null)
            {
                throw Validation("Tenant details patch payload is required.");
            }

            if (!string.IsNullOrWhiteSpace(request.Status)
                && !TenantStatuses.All.Contains(request.Status))
            {
                throw Validation("status is invalid.");
            }

            if (!string.IsNullOrWhiteSpace(request.DisplayName) && request.DisplayName.Length > 256)
            {
                throw Validation("display_name cannot exceed 256 characters.");
            }

            if (!string.IsNullOrWhiteSpace(request.LegalName) && request.LegalName.Length > 256)
            {
                throw Validation("legal_name cannot exceed 256 characters.");
            }
        }

        public void ValidateBillingPatch(TenantBillingPatchRequest request)
        {
            if (request == null)
            {
                throw Validation("Billing patch payload is required.");
            }

            if (!string.IsNullOrWhiteSpace(request.Provider) && !BillingProviders.All.Contains(request.Provider))
            {
                throw Validation("billing.provider is invalid.");
            }

            if (!string.IsNullOrWhiteSpace(request.BillingStatus) && !BillingStatuses.All.Contains(request.BillingStatus))
            {
                throw Validation("billing.billing_status is invalid.");
            }

            if (!string.IsNullOrWhiteSpace(request.Currency) && request.Currency.Length != 3)
            {
                throw Validation("billing.currency must be a 3-letter currency code.");
            }

            if (!string.IsNullOrWhiteSpace(request.BillingEmail))
            {
                try
                {
                    _ = new MailAddress(request.BillingEmail);
                }
                catch
                {
                    throw Validation("billing.billing_email is invalid.");
                }
            }
        }

        public void ValidateAdministrationPatch(TenantAdministrationPatchRequest request)
        {
            if (request == null)
            {
                throw Validation("Administration patch payload is required.");
            }

            if (!string.IsNullOrWhiteSpace(request.Mode)
                && !TenantAdminModes.All.Contains(request.Mode))
            {
                throw Validation("productivity_suite_administration.mode is invalid.");
            }

            if (!string.IsNullOrWhiteSpace(request.Provider)
                && !TenantProviders.All.Contains(request.Provider))
            {
                throw Validation("productivity_suite_administration.provider is invalid.");
            }
        }

        public void ValidateIdentityPatch(TenantIdentityPatchRequest request, TenantDocumentDTO current)
        {
            if (request == null)
            {
                throw Validation("Identity patch payload is required.");
            }

            var activeProvider = current.Administration.Provider ?? TenantProviders.Microsoft;
            var hasMicrosoft = request.Microsoft != null;
            var hasGoogle = request.Google != null;

            if (!hasMicrosoft && !hasGoogle)
            {
                throw Validation("Identity patch must include microsoft or google payload.");
            }

            if (hasMicrosoft && hasGoogle)
            {
                throw Validation("Identity patch cannot include both microsoft and google payloads.");
            }

            if (activeProvider.Equals(TenantProviders.Microsoft, StringComparison.OrdinalIgnoreCase) && hasGoogle)
            {
                throw new TenantApiException(
                    HttpStatusCode.UnprocessableEntity,
                    TenantErrorCodes.ProviderMismatch,
                    "Identity payload provider does not match active provider.");
            }

            if (activeProvider.Equals(TenantProviders.Google, StringComparison.OrdinalIgnoreCase) && hasMicrosoft)
            {
                throw new TenantApiException(
                    HttpStatusCode.UnprocessableEntity,
                    TenantErrorCodes.ProviderMismatch,
                    "Identity payload provider does not match active provider.");
            }
        }

        public void ValidateEmailIntegrationPatch(TenantEmailIntegrationPatchRequest request, TenantDocumentDTO current)
        {
            if (request == null)
            {
                throw Validation("Email-integration patch payload is required.");
            }

            var activeProvider = current.Administration.Provider ?? TenantProviders.Microsoft;
            var hasGraph = request.Graph != null;
            var hasGmail = request.Gmail != null;

            if (hasGraph && hasGmail)
            {
                throw Validation("Email integration patch cannot include both graph and gmail payloads.");
            }

            if (activeProvider.Equals(TenantProviders.Microsoft, StringComparison.OrdinalIgnoreCase) && hasGmail)
            {
                throw new TenantApiException(
                    HttpStatusCode.UnprocessableEntity,
                    TenantErrorCodes.ProviderMismatch,
                    "Email-integration payload provider does not match active provider.");
            }

            if (activeProvider.Equals(TenantProviders.Google, StringComparison.OrdinalIgnoreCase) && hasGraph)
            {
                throw new TenantApiException(
                    HttpStatusCode.UnprocessableEntity,
                    TenantErrorCodes.ProviderMismatch,
                    "Email-integration payload provider does not match active provider.");
            }
        }

        public void ValidateIfMatch(string? ifMatchHeader)
        {
            if (string.IsNullOrWhiteSpace(ifMatchHeader))
            {
                throw new TenantApiException(
                    HttpStatusCode.BadRequest,
                    TenantErrorCodes.MissingIfMatch,
                    "If-Match header is required.");
            }
        }

        private static TenantApiException Validation(string message)
        {
            return new TenantApiException(
                HttpStatusCode.UnprocessableEntity,
                TenantErrorCodes.ValidationFailed,
                message);
        }
    }
}
