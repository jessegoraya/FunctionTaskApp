using System.Net;
using System.Net.Mail;
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
                    Graph = new TenantGraphIntegrationDTO { Enabled = false, EmailIngestionEnabled = false },
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

        public async Task<TenantMarketCodesResponse> GetMarketCodesAsync(
            string tenantId,
            TenantAuthContext auth,
            CancellationToken cancellationToken = default)
        {
            _authorization.EnsureCanReadMarketCodes(auth, tenantId);
            var (document, eTag) = await RequireTenantAsync(tenantId, cancellationToken);

            var canManage = auth.Roles.Any(role =>
                    role.Equals(TenantRoles.TaslowAdmin, StringComparison.OrdinalIgnoreCase)
                    || role.Equals(TenantRoles.TenantAdmin, StringComparison.OrdinalIgnoreCase))
                || auth.Role.Equals(TenantRoles.TaslowAdmin, StringComparison.OrdinalIgnoreCase)
                || auth.Role.Equals(TenantRoles.TenantAdmin, StringComparison.OrdinalIgnoreCase);

            var marketCodes = (document.MarketCodes ?? new List<TenantMarketCodeDTO>())
                .Where(item => canManage || item.IsActive)
                .OrderBy(item => item.DisplayOrder)
                .ThenBy(item => item.Name)
                .ToList();

            return new TenantMarketCodesResponse
            {
                TenantId = tenantId,
                ETag = eTag,
                MarketCodes = marketCodes
            };
        }

        public async Task<TenantMarketCodesResponse> PatchMarketCodesAsync(
            string tenantId,
            TenantMarketCodesPatchRequest request,
            string ifMatch,
            TenantAuthContext auth,
            CancellationToken cancellationToken = default)
        {
            _authorization.EnsureCanManageMarketCodes(auth, tenantId);
            _validation.ValidateIfMatch(ifMatch);

            var normalized = (request?.MarketCodes ?? new List<TenantMarketCodeDTO>())
                .Select(item => new TenantMarketCodeDTO
                {
                    Code = NormalizeMarketCode(item.Code),
                    Name = item.Name?.Trim() ?? string.Empty,
                    Description = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim(),
                    IsActive = item.IsActive,
                    DisplayOrder = item.DisplayOrder
                })
                .ToList();

            if (normalized.Any(item => string.IsNullOrWhiteSpace(item.Code) || string.IsNullOrWhiteSpace(item.Name)))
            {
                throw new TenantApiException(HttpStatusCode.UnprocessableEntity, TenantErrorCodes.BadRequest, "Market Code and name are required.");
            }

            if (normalized.GroupBy(item => item.Code, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            {
                throw new TenantApiException(HttpStatusCode.UnprocessableEntity, TenantErrorCodes.BadRequest, "Market Codes must be unique within the tenant.");
            }

            var (document, _) = await RequireTenantAsync(tenantId, cancellationToken);
            var existingCodes = (document.MarketCodes ?? new List<TenantMarketCodeDTO>())
                .Select(item => item.Code)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .ToList();
            var removedCode = existingCodes.FirstOrDefault(code =>
                !normalized.Any(item => item.Code.Equals(code, StringComparison.OrdinalIgnoreCase)));
            if (!string.IsNullOrWhiteSpace(removedCode))
            {
                throw new TenantApiException(
                    HttpStatusCode.Conflict,
                    TenantErrorCodes.ImmutableField,
                    $"Market Code {removedCode} cannot be deleted. Deactivate it instead.");
            }

            document.MarketCodes = normalized;
            document.SchemaVersion = "1.1.0";
            document.Tenant.UpdatedAt = DateTime.UtcNow.ToString("O");
            var (updated, eTag) = await _repository.ReplaceAsync(document, ifMatch, cancellationToken);

            return new TenantMarketCodesResponse
            {
                TenantId = tenantId,
                ETag = eTag,
                MarketCodes = updated.MarketCodes
                    .OrderBy(item => item.DisplayOrder)
                    .ThenBy(item => item.Name)
                    .ToList()
            };
        }

        public async Task<TenantLeaderMarketCodesResponse> GetLeaderMarketCodesAsync(
            string tenantId,
            string userId,
            TenantAuthContext auth,
            CancellationToken cancellationToken = default)
        {
            var (document, eTag) = await RequireTenantAsync(tenantId, cancellationToken);
            var user = FindTenantUser(document, userId);
            if (user == null)
            {
                throw new TenantApiException(HttpStatusCode.NotFound, TenantErrorCodes.NotFound, "Tenant user not found.");
            }

            var isSelf = !string.IsNullOrWhiteSpace(auth.Email)
                && auth.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase)
                && tenantId.Equals(auth.TenantId, StringComparison.OrdinalIgnoreCase);
            if (!isSelf)
            {
                _authorization.EnsureCanManageMarketCodes(auth, tenantId);
            }

            return new TenantLeaderMarketCodesResponse
            {
                TenantId = tenantId,
                ETag = eTag,
                User = user
            };
        }

        public async Task<TenantLeaderMarketCodesResponse> PatchLeaderMarketCodesAsync(
            string tenantId,
            string userId,
            TenantLeaderMarketCodesPatchRequest request,
            string ifMatch,
            TenantAuthContext auth,
            CancellationToken cancellationToken = default)
        {
            _authorization.EnsureCanManageMarketCodes(auth, tenantId);
            _validation.ValidateIfMatch(ifMatch);

            var (document, _) = await RequireTenantAsync(tenantId, cancellationToken);
            document.TenantUsers ??= new List<TenantUserMembershipDTO>();

            var codes = (request?.LeaderMarketCodes ?? new List<string>())
                .Select(NormalizeMarketCode)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(code => code)
                .ToList();

            var currentUser = FindTenantUser(document, userId);
            var currentCodes = currentUser?.LeaderMarketCodes ?? new List<string>();
            var allowedCodes = (document.MarketCodes ?? new List<TenantMarketCodeDTO>())
                .Where(item => item.IsActive || currentCodes.Contains(item.Code, StringComparer.OrdinalIgnoreCase))
                .Select(item => item.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var invalidCode = codes.FirstOrDefault(code => !allowedCodes.Contains(code));
            if (!string.IsNullOrWhiteSpace(invalidCode))
            {
                throw new TenantApiException(
                    HttpStatusCode.UnprocessableEntity,
                    TenantErrorCodes.BadRequest,
                    $"Market Code {invalidCode} is not active for this tenant.");
            }

            if (currentUser == null)
            {
                if (string.IsNullOrWhiteSpace(request?.Email) || string.IsNullOrWhiteSpace(request.DisplayName))
                {
                    throw new TenantApiException(
                        HttpStatusCode.UnprocessableEntity,
                        TenantErrorCodes.BadRequest,
                        "displayName and email are required when creating an explicit tenant leader.");
                }

                ValidateEmail(request.Email);
                currentUser = new TenantUserMembershipDTO
                {
                    UserId = userId.Trim(),
                    DisplayName = request.DisplayName.Trim(),
                    Email = request.Email.Trim().ToLowerInvariant(),
                    Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
                    IsActive = true
                };
                document.TenantUsers.Add(currentUser);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(request?.DisplayName))
                {
                    currentUser.DisplayName = request.DisplayName.Trim();
                }

                if (!string.IsNullOrWhiteSpace(request?.Email))
                {
                    ValidateEmail(request.Email);
                    currentUser.Email = request.Email.Trim().ToLowerInvariant();
                }

                if (request?.Title != null)
                {
                    currentUser.Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim();
                }
            }

            currentUser.LeaderMarketCodes = codes;
            currentUser.IsActive = true;
            document.SchemaVersion = "1.1.0";
            document.Tenant.UpdatedAt = DateTime.UtcNow.ToString("O");
            var (updated, eTag) = await _repository.ReplaceAsync(document, ifMatch, cancellationToken);

            return new TenantLeaderMarketCodesResponse
            {
                TenantId = tenantId,
                ETag = eTag,
                User = FindTenantUser(updated, userId) ?? currentUser
            };
        }

        private async Task<(TenantDocumentDTO Document, string ETag)> RequireTenantAsync(
            string tenantId,
            CancellationToken cancellationToken)
        {
            var (document, eTag) = await _repository.GetByIdAsync(tenantId, cancellationToken);
            if (document == null || string.IsNullOrWhiteSpace(eTag))
            {
                throw new TenantApiException(HttpStatusCode.NotFound, TenantErrorCodes.NotFound, "Tenant not found.");
            }

            document.MarketCodes ??= new List<TenantMarketCodeDTO>();
            document.TenantUsers ??= new List<TenantUserMembershipDTO>();
            return (document, eTag);
        }

        private static TenantUserMembershipDTO? FindTenantUser(TenantDocumentDTO document, string userId)
        {
            var normalized = userId?.Trim() ?? string.Empty;
            return (document.TenantUsers ?? new List<TenantUserMembershipDTO>()).FirstOrDefault(user =>
                user.UserId.Equals(normalized, StringComparison.OrdinalIgnoreCase)
                || user.Email.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeMarketCode(string? value)
        {
            var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
            if (normalized.Any(character => !char.IsLetterOrDigit(character) && character != '-' && character != '_'))
            {
                throw new TenantApiException(
                    HttpStatusCode.UnprocessableEntity,
                    TenantErrorCodes.BadRequest,
                    $"Invalid Market Code: {value}.");
            }

            return normalized;
        }

        private static void ValidateEmail(string email)
        {
            try
            {
                _ = new MailAddress(email);
            }
            catch
            {
                throw new TenantApiException(HttpStatusCode.UnprocessableEntity, TenantErrorCodes.BadRequest, "Tenant user email is invalid.");
            }
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
