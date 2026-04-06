using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Taslow.Shared.Model;
using Taslow.Tenant.Model;
using Taslow.Tenant.Service.Interface;

namespace Taslow.Tenant.Service
{
    public class TenantValidationService : ITenantValidationService
    {
        private const int TenantDisplayNameMaxLength = 256;
        private const int TenantLegalNameMaxLength = 256;
        private const int CompanyPocNameMaxLength = 120;
        private const int CompanyPocTitleMaxLength = 120;
        private const int CompanyPocEmailMaxLength = 254;
        private const int CompanyPocPhoneMaxLength = 32;
        private const int MailingAddressLineMaxLength = 120;
        private const int MailingCityMaxLength = 80;
        private const int MailingStateProvinceMaxLength = 80;
        private const int MailingPostalCodeMaxLength = 20;
        private static readonly Regex CountryCodeRegex = new("^[A-Z]{2}$", RegexOptions.Compiled);
        private static readonly Regex PhoneRegex = new("^\\+?[0-9()\\-\\.\\s]{7,32}$", RegexOptions.Compiled);

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

            if (request.DisplayName.Length > TenantDisplayNameMaxLength)
            {
                throw Validation($"display_name cannot exceed {TenantDisplayNameMaxLength} characters.");
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

            ValidateRequiredText(request.CompanyPocName, "company_poc_name", CompanyPocNameMaxLength);
            ValidateRequiredText(request.CompanyPocTitle, "company_poc_title", CompanyPocTitleMaxLength);
            ValidateRequiredEmail(request.CompanyPocEmail, "company_poc_email");
            ValidateRequiredPhone(request.CompanyPocPhone, "company_poc_phone");
            ValidateRequiredText(request.MailingAddressLine1, "mailing_address_line1", MailingAddressLineMaxLength);
            ValidateOptionalText(request.MailingAddressLine2, "mailing_address_line2", MailingAddressLineMaxLength);
            ValidateRequiredText(request.MailingCity, "mailing_city", MailingCityMaxLength);
            ValidateRequiredText(request.MailingStateProvince, "mailing_state_province", MailingStateProvinceMaxLength);
            ValidateRequiredText(request.MailingPostalCode, "mailing_postal_code", MailingPostalCodeMaxLength);
            ValidateRequiredCountryCode(request.MailingCountryCode, "mailing_country_code");
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

            if (!string.IsNullOrWhiteSpace(request.DisplayName) && request.DisplayName.Length > TenantDisplayNameMaxLength)
            {
                throw Validation($"display_name cannot exceed {TenantDisplayNameMaxLength} characters.");
            }

            if (!string.IsNullOrWhiteSpace(request.LegalName) && request.LegalName.Length > TenantLegalNameMaxLength)
            {
                throw Validation($"legal_name cannot exceed {TenantLegalNameMaxLength} characters.");
            }

            ValidatePatchText(request.CompanyPocName, "company_poc_name", CompanyPocNameMaxLength);
            ValidatePatchText(request.CompanyPocTitle, "company_poc_title", CompanyPocTitleMaxLength);
            ValidatePatchEmail(request.CompanyPocEmail, "company_poc_email");
            ValidatePatchPhone(request.CompanyPocPhone, "company_poc_phone");
            ValidatePatchText(request.MailingAddressLine1, "mailing_address_line1", MailingAddressLineMaxLength);
            ValidateOptionalText(request.MailingAddressLine2, "mailing_address_line2", MailingAddressLineMaxLength);
            ValidatePatchText(request.MailingCity, "mailing_city", MailingCityMaxLength);
            ValidatePatchText(request.MailingStateProvince, "mailing_state_province", MailingStateProvinceMaxLength);
            ValidatePatchText(request.MailingPostalCode, "mailing_postal_code", MailingPostalCodeMaxLength);
            ValidatePatchCountryCode(request.MailingCountryCode, "mailing_country_code");
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

        private static void ValidateRequiredText(string? value, string fieldName, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw Validation($"{fieldName} is required.");
            }

            if (value.Trim().Length > maxLength)
            {
                throw Validation($"{fieldName} cannot exceed {maxLength} characters.");
            }
        }

        private static void ValidateOptionalText(string? value, string fieldName, int maxLength)
        {
            if (value == null)
            {
                return;
            }

            if (value.Trim().Length > maxLength)
            {
                throw Validation($"{fieldName} cannot exceed {maxLength} characters.");
            }
        }

        private static void ValidatePatchText(string? value, string fieldName, int maxLength)
        {
            if (value == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw Validation($"{fieldName} cannot be empty.");
            }

            if (value.Trim().Length > maxLength)
            {
                throw Validation($"{fieldName} cannot exceed {maxLength} characters.");
            }
        }

        private static void ValidateRequiredEmail(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw Validation($"{fieldName} is required.");
            }

            if (value.Trim().Length > CompanyPocEmailMaxLength)
            {
                throw Validation($"{fieldName} cannot exceed {CompanyPocEmailMaxLength} characters.");
            }

            try
            {
                _ = new MailAddress(value.Trim());
            }
            catch
            {
                throw Validation($"{fieldName} is invalid.");
            }
        }

        private static void ValidatePatchEmail(string? value, string fieldName)
        {
            if (value == null)
            {
                return;
            }

            ValidateRequiredEmail(value, fieldName);
        }

        private static void ValidateRequiredPhone(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw Validation($"{fieldName} is required.");
            }

            var trimmed = value.Trim();
            if (trimmed.Length > CompanyPocPhoneMaxLength)
            {
                throw Validation($"{fieldName} cannot exceed {CompanyPocPhoneMaxLength} characters.");
            }

            if (!PhoneRegex.IsMatch(trimmed))
            {
                throw Validation($"{fieldName} is invalid.");
            }
        }

        private static void ValidatePatchPhone(string? value, string fieldName)
        {
            if (value == null)
            {
                return;
            }

            ValidateRequiredPhone(value, fieldName);
        }

        private static void ValidateRequiredCountryCode(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw Validation($"{fieldName} is required.");
            }

            var normalized = value.Trim().ToUpperInvariant();
            if (!CountryCodeRegex.IsMatch(normalized))
            {
                throw Validation($"{fieldName} must be a 2-letter uppercase ISO country code.");
            }
        }

        private static void ValidatePatchCountryCode(string? value, string fieldName)
        {
            if (value == null)
            {
                return;
            }

            ValidateRequiredCountryCode(value, fieldName);
        }
    }
}
