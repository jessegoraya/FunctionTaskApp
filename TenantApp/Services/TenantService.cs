using System.Net;
using Taslow.Shared.Model;
using Taslow.Tenant.DAL.Interface;
using Taslow.Tenant.Model;
using Taslow.Tenant.Service.Interface;

namespace Taslow.Tenant.Service
{
    public class TenantService : ITenantService
    {
        private readonly ITenantRepository _repository;
        private readonly ITenantValidationService _validation;
        private readonly ITenantAuthorizationService _authorization;

        public TenantService(
            ITenantRepository repository,
            ITenantValidationService validation,
            ITenantAuthorizationService authorization)
        {
            _repository = repository;
            _validation = validation;
            _authorization = authorization;
        }

        public async Task<TenantListResponse> ListAsync(TenantListQuery query, TenantAuthContext auth, CancellationToken cancellationToken = default)
        {
            _authorization.EnsureCanList(auth);

            var normalizedQuery = new TenantListQuery
            {
                Status = string.IsNullOrWhiteSpace(query.Status) ? TenantStatuses.Active : query.Status,
                Search = query.Search?.Trim(),
                PageSize = query.PageSize <= 0 ? 25 : Math.Min(query.PageSize, 100),
                ContinuationToken = query.ContinuationToken
            };

            var (items, continuationToken) = await _repository.ListAsync(normalizedQuery, cancellationToken);
            return new TenantListResponse
            {
                Items = items.Select(MapListItem).ToList(),
                ContinuationToken = continuationToken
            };
        }

        public async Task<TenantDetailResponse> GetByIdAsync(string tenantId, TenantAuthContext auth, CancellationToken cancellationToken = default)
        {
            _authorization.EnsureCanReadOrUpdateTenant(auth, tenantId);
            var (document, eTag) = await _repository.GetByIdAsync(tenantId, cancellationToken);
            if (document == null || string.IsNullOrWhiteSpace(eTag))
            {
                throw new TenantApiException(HttpStatusCode.NotFound, TenantErrorCodes.NotFound, "Tenant not found.");
            }

            return new TenantDetailResponse
            {
                TenantId = tenantId,
                ETag = eTag,
                Data = document
            };
        }

        public async Task<TenantDetailResponse> CreateAsync(TenantCreateRequest request, TenantAuthContext auth, CancellationToken cancellationToken = default)
        {
            _authorization.EnsureCanCreate(auth);
            _validation.ValidateCreateRequest(request);

            var now = DateTime.UtcNow.ToString("O");
            var tenantId = Guid.NewGuid().ToString();
            var status = string.IsNullOrWhiteSpace(request.Status) ? TenantStatuses.Provisioning : request.Status!.ToLowerInvariant();
            var provider = string.IsNullOrWhiteSpace(request.Provider) ? TenantProviders.Microsoft : request.Provider!.ToLowerInvariant();

            var document = new TenantDocumentDTO
            {
                Id = tenantId,
                SchemaVersion = "1.0.0",
                Tenant = new TenantCoreDTO
                {
                    TenantId = tenantId,
                    Status = status,
                    DisplayName = request.DisplayName.Trim(),
                    LegalName = request.LegalName?.Trim(),
                    CompanyPocName = request.CompanyPocName.Trim(),
                    CompanyPocTitle = request.CompanyPocTitle.Trim(),
                    CompanyPocEmail = request.CompanyPocEmail.Trim(),
                    CompanyPocPhone = request.CompanyPocPhone.Trim(),
                    MailingAddressLine1 = request.MailingAddressLine1.Trim(),
                    MailingAddressLine2 = string.IsNullOrWhiteSpace(request.MailingAddressLine2)
                        ? null
                        : request.MailingAddressLine2.Trim(),
                    MailingCity = request.MailingCity.Trim(),
                    MailingStateProvince = request.MailingStateProvince.Trim(),
                    MailingPostalCode = request.MailingPostalCode.Trim(),
                    MailingCountryCode = request.MailingCountryCode.Trim().ToUpperInvariant(),
                    CreatedAt = now,
                    UpdatedAt = now,
                    EntitlementsJson = new Dictionary<string, object>()
                },
                Billing = request.Billing ?? new TenantBillingPatchRequest
                {
                    Provider = BillingProviders.Other,
                    BillingStatus = BillingStatuses.Trialing,
                    PlanId = "starter",
                    Currency = "USD"
                },
                Administration = request.Administration ?? new TenantAdministrationPatchRequest
                {
                    Mode = TenantAdminModes.ExternalGroup,
                    Provider = provider,
                    BreakGlassEnabled = true
                },
                Identity = request.Identity ?? new TenantIdentityPatchRequest(),
                EmailIntegration = request.EmailIntegration ?? new TenantEmailIntegrationPatchRequest
                {
                    Graph = new TenantGraphIntegrationDTO { Enabled = false },
                    Gmail = new TenantGmailIntegrationDTO { Enabled = false },
                    MailboxStates = new List<TenantMailboxStateDTO>(),
                    SubscriptionRegistry = new List<TenantSubscriptionRegistryItemDTO>()
                }
            };

            if (document.Administration.Provider?.Equals(TenantProviders.Microsoft, StringComparison.OrdinalIgnoreCase) == true
                && document.Identity.Microsoft == null)
            {
                document.Identity.Microsoft = new TenantMicrosoftIdentityDTO();
            }

            if (document.Administration.Provider?.Equals(TenantProviders.Google, StringComparison.OrdinalIgnoreCase) == true
                && document.Identity.Google == null)
            {
                document.Identity.Google = new TenantGoogleIdentityDTO();
            }

            _validation.ValidateBillingPatch(document.Billing);
            _validation.ValidateAdministrationPatch(document.Administration);

            var (created, eTag) = await _repository.CreateAsync(document, cancellationToken);
            return new TenantDetailResponse
            {
                TenantId = tenantId,
                ETag = eTag,
                Data = created
            };
        }

        public async Task<TenantDetailResponse> PatchTenantAsync(string tenantId, TenantDetailsPatchRequest request, string ifMatch, TenantAuthContext auth, CancellationToken cancellationToken = default)
        {
            _authorization.EnsureCanReadOrUpdateTenant(auth, tenantId);
            _validation.ValidateIfMatch(ifMatch);
            _validation.ValidateDetailsPatch(request);

            return await UpdateDocumentAsync(tenantId, ifMatch, document =>
            {
                if (!string.IsNullOrWhiteSpace(request.DisplayName))
                {
                    document.Tenant.DisplayName = request.DisplayName.Trim();
                }

                if (request.LegalName != null)
                {
                    document.Tenant.LegalName = string.IsNullOrWhiteSpace(request.LegalName) ? null : request.LegalName.Trim();
                }

                if (!string.IsNullOrWhiteSpace(request.Status))
                {
                    document.Tenant.Status = request.Status!.ToLowerInvariant();
                }

                if (request.EntitlementsJson != null)
                {
                    document.Tenant.EntitlementsJson = request.EntitlementsJson;
                }

                if (request.CompanyPocName != null)
                {
                    document.Tenant.CompanyPocName = request.CompanyPocName.Trim();
                }

                if (request.CompanyPocTitle != null)
                {
                    document.Tenant.CompanyPocTitle = request.CompanyPocTitle.Trim();
                }

                if (request.CompanyPocEmail != null)
                {
                    document.Tenant.CompanyPocEmail = request.CompanyPocEmail.Trim();
                }

                if (request.CompanyPocPhone != null)
                {
                    document.Tenant.CompanyPocPhone = request.CompanyPocPhone.Trim();
                }

                if (request.MailingAddressLine1 != null)
                {
                    document.Tenant.MailingAddressLine1 = request.MailingAddressLine1.Trim();
                }

                if (request.MailingAddressLine2 != null)
                {
                    document.Tenant.MailingAddressLine2 = string.IsNullOrWhiteSpace(request.MailingAddressLine2)
                        ? null
                        : request.MailingAddressLine2.Trim();
                }

                if (request.MailingCity != null)
                {
                    document.Tenant.MailingCity = request.MailingCity.Trim();
                }

                if (request.MailingStateProvince != null)
                {
                    document.Tenant.MailingStateProvince = request.MailingStateProvince.Trim();
                }

                if (request.MailingPostalCode != null)
                {
                    document.Tenant.MailingPostalCode = request.MailingPostalCode.Trim();
                }

                if (request.MailingCountryCode != null)
                {
                    document.Tenant.MailingCountryCode = request.MailingCountryCode.Trim().ToUpperInvariant();
                }
            }, cancellationToken);
        }

        public async Task<TenantDetailResponse> PatchBillingAsync(string tenantId, TenantBillingPatchRequest request, string ifMatch, TenantAuthContext auth, CancellationToken cancellationToken = default)
        {
            _authorization.EnsureCanReadOrUpdateTenant(auth, tenantId);
            _validation.ValidateIfMatch(ifMatch);
            _validation.ValidateBillingPatch(request);

            return await UpdateDocumentAsync(tenantId, ifMatch, document =>
            {
                document.Billing.Provider = request.Provider ?? document.Billing.Provider;
                document.Billing.ProviderCustomerId = request.ProviderCustomerId ?? document.Billing.ProviderCustomerId;
                document.Billing.ProviderSubscriptionId = request.ProviderSubscriptionId ?? document.Billing.ProviderSubscriptionId;
                document.Billing.BillingStatus = request.BillingStatus ?? document.Billing.BillingStatus;
                document.Billing.PlanId = request.PlanId ?? document.Billing.PlanId;
                document.Billing.Currency = request.Currency ?? document.Billing.Currency;
                document.Billing.BillingEmail = request.BillingEmail ?? document.Billing.BillingEmail;
            }, cancellationToken);
        }

        public async Task<TenantDetailResponse> PatchAdministrationAsync(string tenantId, TenantAdministrationPatchRequest request, string ifMatch, TenantAuthContext auth, CancellationToken cancellationToken = default)
        {
            _authorization.EnsureCanReadOrUpdateTenant(auth, tenantId);
            _validation.ValidateIfMatch(ifMatch);
            _validation.ValidateAdministrationPatch(request);

            return await UpdateDocumentAsync(tenantId, ifMatch, document =>
            {
                document.Administration.Mode = request.Mode ?? document.Administration.Mode;
                document.Administration.Provider = request.Provider ?? document.Administration.Provider;
                document.Administration.ExternalGroupKey = request.ExternalGroupKey ?? document.Administration.ExternalGroupKey;
                document.Administration.BreakGlassEnabled = request.BreakGlassEnabled ?? document.Administration.BreakGlassEnabled;
                document.Administration.LastPolicyVerifiedAt = request.LastPolicyVerifiedAt ?? document.Administration.LastPolicyVerifiedAt;
            }, cancellationToken);
        }

        public async Task<TenantDetailResponse> PatchIdentityAsync(string tenantId, TenantIdentityPatchRequest request, string ifMatch, TenantAuthContext auth, CancellationToken cancellationToken = default)
        {
            _authorization.EnsureCanReadOrUpdateTenant(auth, tenantId);
            _validation.ValidateIfMatch(ifMatch);

            var (current, _) = await _repository.GetByIdAsync(tenantId, cancellationToken);
            if (current == null)
            {
                throw new TenantApiException(HttpStatusCode.NotFound, TenantErrorCodes.NotFound, "Tenant not found.");
            }

            _validation.ValidateIdentityPatch(request, current);

            return await UpdateDocumentAsync(tenantId, ifMatch, document =>
            {
                if (request.Microsoft != null)
                {
                    document.Identity.Microsoft = request.Microsoft;
                }

                if (request.Google != null)
                {
                    document.Identity.Google = request.Google;
                }
            }, cancellationToken);
        }

        public async Task<TenantDetailResponse> PatchEmailIntegrationAsync(string tenantId, TenantEmailIntegrationPatchRequest request, string ifMatch, TenantAuthContext auth, CancellationToken cancellationToken = default)
        {
            _authorization.EnsureCanReadOrUpdateTenant(auth, tenantId);
            _validation.ValidateIfMatch(ifMatch);

            var (current, _) = await _repository.GetByIdAsync(tenantId, cancellationToken);
            if (current == null)
            {
                throw new TenantApiException(HttpStatusCode.NotFound, TenantErrorCodes.NotFound, "Tenant not found.");
            }

            _validation.ValidateEmailIntegrationPatch(request, current);

            return await UpdateDocumentAsync(tenantId, ifMatch, document =>
            {
                if (request.Graph != null)
                {
                    document.EmailIntegration.Graph = request.Graph;
                }

                if (request.Gmail != null)
                {
                    document.EmailIntegration.Gmail = request.Gmail;
                }

                if (request.MailboxStates != null)
                {
                    document.EmailIntegration.MailboxStates = request.MailboxStates;
                }

                if (request.SubscriptionRegistry != null)
                {
                    document.EmailIntegration.SubscriptionRegistry = request.SubscriptionRegistry;
                }
            }, cancellationToken);
        }

        private async Task<TenantDetailResponse> UpdateDocumentAsync(
            string tenantId,
            string ifMatch,
            Action<TenantDocumentDTO> updater,
            CancellationToken cancellationToken)
        {
            var (document, _) = await _repository.GetByIdAsync(tenantId, cancellationToken);
            if (document == null)
            {
                throw new TenantApiException(HttpStatusCode.NotFound, TenantErrorCodes.NotFound, "Tenant not found.");
            }

            // Guard immutable identifiers.
            if (!document.Id.Equals(document.Tenant.TenantId, StringComparison.OrdinalIgnoreCase))
            {
                throw new TenantApiException(
                    HttpStatusCode.Conflict,
                    TenantErrorCodes.ImmutableField,
                    "Tenant identifiers are inconsistent.");
            }

            updater(document);
            document.Id = tenantId;
            document.Tenant.TenantId = tenantId;
            document.Tenant.UpdatedAt = DateTime.UtcNow.ToString("O");

            var (updated, eTag) = await _repository.ReplaceAsync(document, ifMatch, cancellationToken);
            return new TenantDetailResponse
            {
                TenantId = tenantId,
                ETag = eTag,
                Data = updated
            };
        }

        private static TenantListItemDTO MapListItem(TenantDocumentDTO item)
        {
            return new TenantListItemDTO
            {
                TenantId = item.Tenant.TenantId,
                DisplayName = item.Tenant.DisplayName,
                Status = item.Tenant.Status,
                Provider = item.Administration.Provider ?? TenantProviders.Microsoft,
                UpdatedAt = item.Tenant.UpdatedAt
            };
        }
    }
}
