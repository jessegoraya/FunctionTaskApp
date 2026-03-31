using Taslow.Shared.Model;

namespace Taslow.Tenant.Service.Interface
{
    public interface ITenantValidationService
    {
        void ValidateCreateRequest(TenantCreateRequest request);
        void ValidateDetailsPatch(TenantDetailsPatchRequest request);
        void ValidateBillingPatch(TenantBillingPatchRequest request);
        void ValidateAdministrationPatch(TenantAdministrationPatchRequest request);
        void ValidateIdentityPatch(TenantIdentityPatchRequest request, TenantDocumentDTO current);
        void ValidateEmailIntegrationPatch(TenantEmailIntegrationPatchRequest request, TenantDocumentDTO current);
        void ValidateIfMatch(string? ifMatchHeader);
    }
}
