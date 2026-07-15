using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Taslow.Shared.Model;
using Taslow.Tenant.DAL.Interface;
using Taslow.Tenant.Model;
using Taslow.Tenant.Service.Interface;

namespace Taslow.Tenant.Service
{
    public class TenantAuthService : ITenantAuthService
    {
        private readonly ITenantRepository _tenantRepository;
        private readonly ITenantAuthorizationService _authorizationService;
        private readonly IAuthenticationAuditRepository _auditRepository;
        private readonly ITaslowJwtService _jwtService;
        private readonly ITenantUserCatalogService _tenantUserCatalog;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TenantAuthService> _logger;
        private static readonly System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler ProviderTokenHandler = new();

        public TenantAuthService(
            ITenantRepository tenantRepository,
            ITenantAuthorizationService authorizationService,
            IAuthenticationAuditRepository auditRepository,
            ITaslowJwtService jwtService,
            ITenantUserCatalogService tenantUserCatalog,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<TenantAuthService> logger)
        {
            _tenantRepository = tenantRepository;
            _authorizationService = authorizationService;
            _auditRepository = auditRepository;
            _jwtService = jwtService;
            _tenantUserCatalog = tenantUserCatalog;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<DevSessionResponse> CreateDevSessionAsync(
            DevSessionRequest request,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            if (IsProduction())
            {
                throw new TenantApiException(
                    HttpStatusCode.Forbidden,
                    AuthErrorCodes.SyntheticNotAllowedInProduction,
                    "Synthetic login is not available in production.");
            }

            if (!IsSyntheticLoginEnabled())
            {
                throw new TenantApiException(
                    HttpStatusCode.Forbidden,
                    AuthErrorCodes.SyntheticDisabled,
                    "Synthetic login is disabled for this environment.");
            }

            if (string.IsNullOrWhiteSpace(request.TenantId))
            {
                throw new TenantApiException(HttpStatusCode.BadRequest, TenantErrorCodes.BadRequest, "tenantId is required.");
            }

            if (!IdentityModes.Synthetic.Equals(request.IdentityMode, StringComparison.OrdinalIgnoreCase))
            {
                throw new TenantApiException(
                    HttpStatusCode.BadRequest,
                    TenantErrorCodes.BadRequest,
                    "Dev session endpoint only supports synthetic identity mode.");
            }

            var (tenant, _) = await _tenantRepository.GetByIdAsync(request.TenantId, cancellationToken);
            if (tenant == null)
            {
                throw new TenantApiException(HttpStatusCode.NotFound, AuthErrorCodes.TenantNotFound, "Tenant not found.");
            }

            if (!tenant.Tenant.Status.Equals(TenantStatuses.Active, StringComparison.OrdinalIgnoreCase))
            {
                throw new TenantApiException(HttpStatusCode.Forbidden, AuthErrorCodes.TenantNotActive, "Tenant is not active.");
            }

            if (!IsSyntheticTenant(tenant))
            {
                throw new TenantApiException(
                    HttpStatusCode.Forbidden,
                    AuthErrorCodes.ProviderRequired,
                    "Selected tenant has an external provider binding. Provider sign-in is required.");
            }

            if (string.IsNullOrWhiteSpace(request.SelectedUserId))
            {
                throw new TenantApiException(HttpStatusCode.BadRequest, AuthErrorCodes.UserNotSelectable, "Select a tenant user before signing in.");
            }

            var selectableUsers = await _tenantUserCatalog.GetSelectableUsersAsync(request.TenantId, cancellationToken);
            var selectedUser = selectableUsers.FirstOrDefault(user => IsSelectedUser(user, request.SelectedUserId));
            if (selectedUser == null)
            {
                throw new TenantApiException(
                    HttpStatusCode.Forbidden,
                    AuthErrorCodes.UserNotSelectable,
                    "Selected user is not available for this tenant.");
            }

            var roles = selectedUser.Roles
                .Where(role => TenantRoles.All.Contains(role))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (roles.Count == 0)
            {
                roles.Add(TenantRoles.TenantUser);
            }

            var primaryRole = roles.Contains(TenantRoles.TenantPm, StringComparer.OrdinalIgnoreCase)
                ? TenantRoles.TenantPm
                : roles[0];

            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(GetSessionLifetimeMinutes());
            var auth = new TenantAuthContext
            {
                Role = primaryRole,
                TenantId = request.TenantId,
                Subject = selectedUser.Subject,
                DisplayName = selectedUser.DisplayName,
                Email = selectedUser.Email,
                Provider = AuthProviders.Synthetic,
                IdentityMode = IdentityModes.Synthetic,
                Environment = EnvironmentName,
                Jti = Guid.NewGuid().ToString(),
                ExpiresAt = expiresAt.UtcDateTime.ToString("O"),
                Impersonated = true,
                Roles = roles,
                LeaderMarketCodes = selectedUser.LeaderMarketCodes
            };

            var token = _jwtService.IssueToken(auth, expiresAt);
            var response = new DevSessionResponse
            {
                AccessToken = token,
                Session = await BuildContextResponseAsync(auth, cancellationToken)
            };

            await WriteAuditAsync(auth, AuthAuditEventTypes.LoginSucceeded, correlationId, cancellationToken);
            return response;
        }

        public async Task<ProviderLoginStartResponse> StartProviderLoginAsync(
            ProviderLoginStartRequest request,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.TenantId))
            {
                throw new TenantApiException(HttpStatusCode.BadRequest, TenantErrorCodes.BadRequest, "tenantId is required.");
            }

            if (string.IsNullOrWhiteSpace(request.SelectedUserId))
            {
                throw new TenantApiException(HttpStatusCode.BadRequest, AuthErrorCodes.UserNotSelectable, "Select a tenant user before provider sign-in.");
            }

            var (tenant, selectedUser) = await ResolveProviderLoginSelectionAsync(
                request.TenantId,
                request.SelectedUserId,
                cancellationToken);

            var provider = (request.Provider ?? tenant.Administration.Provider ?? TenantProviders.Microsoft).ToLowerInvariant();
            if (!provider.Equals(AuthProviders.Microsoft, StringComparison.OrdinalIgnoreCase))
            {
                throw new TenantApiException(
                    HttpStatusCode.NotImplemented,
                    AuthErrorCodes.ProviderUnsupported,
                    "Only Microsoft 365 provider login is implemented in this phase.");
            }

            var microsoftTenantId = tenant.Identity.Microsoft?.MicrosoftTid?.Trim();
            if (!HasRealProviderBindingValue(microsoftTenantId))
            {
                throw new TenantApiException(
                    HttpStatusCode.Forbidden,
                    AuthErrorCodes.ProviderRequired,
                    "Selected tenant does not have an active Microsoft tenant binding.");
            }

            var clientId = GetRequiredConfiguration("Auth:Microsoft:ClientId", "AzureAd:ClientId");
            var redirectUri = GetMicrosoftRedirectUri();
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(10);
            var state = CreateSignedProviderState(new ProviderLoginState
            {
                TenantId = request.TenantId,
                Provider = AuthProviders.Microsoft,
                SelectedUserId = selectedUser.TenantUserId,
                LoginHint = string.IsNullOrWhiteSpace(request.LoginHint) ? selectedUser.Email : request.LoginHint,
                ReturnUrl = NormalizeReturnUrl(request.ReturnUrl),
                ExpiresAt = expiresAt.UtcDateTime.ToString("O"),
                CorrelationId = correlationId,
                Nonce = Guid.NewGuid().ToString("N")
            });

            return new ProviderLoginStartResponse
            {
                Provider = AuthProviders.Microsoft,
                AuthorizationUrl = BuildMicrosoftAuthorizationUrl(
                    microsoftTenantId!,
                    clientId,
                    redirectUri,
                    selectedUser.Email,
                    state),
                ExpiresAt = expiresAt.UtcDateTime.ToString("O")
            };
        }

        public async Task<ProviderLoginCompletion> CompleteMicrosoftLoginAsync(
            string code,
            string state,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new TenantApiException(HttpStatusCode.BadRequest, TenantErrorCodes.BadRequest, "Microsoft authorization code is required.");
            }

            var loginState = ReadSignedProviderState(state);
            if (!loginState.Provider.Equals(AuthProviders.Microsoft, StringComparison.OrdinalIgnoreCase))
            {
                throw new TenantApiException(
                    HttpStatusCode.BadRequest,
                    AuthErrorCodes.ProviderUnsupported,
                    "Provider callback does not match Microsoft login state.");
            }

            var (tenant, selectedUser) = await ResolveProviderLoginSelectionAsync(
                loginState.TenantId,
                loginState.SelectedUserId,
                cancellationToken);

            var microsoftTenantId = tenant.Identity.Microsoft?.MicrosoftTid?.Trim();
            if (!HasRealProviderBindingValue(microsoftTenantId))
            {
                throw new TenantApiException(
                    HttpStatusCode.Forbidden,
                    AuthErrorCodes.ProviderRequired,
                    "Selected tenant does not have an active Microsoft tenant binding.");
            }

            var clientId = GetRequiredConfiguration("Auth:Microsoft:ClientId", "AzureAd:ClientId");
            var tokenPayload = await ExchangeMicrosoftCodeAsync(
                microsoftTenantId!,
                code,
                clientId,
                GetRequiredConfiguration("Auth:Microsoft:ClientSecret", "AzureAd:ClientSecret"),
                GetMicrosoftRedirectUri(),
                cancellationToken);

            var idToken = tokenPayload.Value<string>("id_token");
            if (string.IsNullOrWhiteSpace(idToken))
            {
                throw new TenantApiException(
                    HttpStatusCode.Unauthorized,
                    AuthErrorCodes.TokenInvalid,
                    "Microsoft token response did not include an id_token.");
            }

            var principal = await ValidateMicrosoftIdTokenAsync(idToken, microsoftTenantId!, clientId, cancellationToken);
            var providerTenantId = FindClaimValue(
                principal,
                "tid",
                "tenantid",
                "http://schemas.microsoft.com/identity/claims/tenantid");
            if (!microsoftTenantId!.Equals(providerTenantId, StringComparison.OrdinalIgnoreCase))
            {
                throw new TenantApiException(
                    HttpStatusCode.Forbidden,
                    AuthErrorCodes.ProviderTenantMismatch,
                    "The authenticated Microsoft tenant does not match the selected Taslow tenant.",
                    BuildProviderTenantMismatchDetails(microsoftTenantId, providerTenantId));
            }

            var providerEmail = FindClaimValue(principal, "preferred_username", "upn", "email", ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(providerEmail)
                || !providerEmail.Equals(selectedUser.Email, StringComparison.OrdinalIgnoreCase))
            {
                throw new TenantApiException(
                    HttpStatusCode.Forbidden,
                    AuthErrorCodes.UserNotInTenant,
                    "The authenticated Microsoft user does not match the selected Taslow tenant user.");
            }

            var roles = selectedUser.Roles
                .Where(role => TenantRoles.All.Contains(role))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (roles.Count == 0)
            {
                roles.Add(TenantRoles.TenantUser);
            }

            var primaryRole = roles.Contains(TenantRoles.TenantPm, StringComparer.OrdinalIgnoreCase)
                ? TenantRoles.TenantPm
                : roles[0];
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(GetSessionLifetimeMinutes());
            var providerSubject = FindClaimValue(
                principal,
                "oid",
                "objectidentifier",
                "http://schemas.microsoft.com/identity/claims/objectidentifier",
                ClaimTypes.NameIdentifier);
            var auth = new TenantAuthContext
            {
                Role = primaryRole,
                TenantId = tenant.Tenant.TenantId,
                Subject = selectedUser.Subject,
                DisplayName = FindClaimValue(principal, "name", ClaimTypes.Name) ?? selectedUser.DisplayName,
                Email = selectedUser.Email,
                Provider = AuthProviders.Microsoft,
                IdentityMode = IdentityModes.Integrated,
                Environment = EnvironmentName,
                ProviderTenantId = microsoftTenantId,
                ProviderSubject = providerSubject,
                Jti = Guid.NewGuid().ToString(),
                ExpiresAt = expiresAt.UtcDateTime.ToString("O"),
                Impersonated = false,
                Roles = roles,
                LeaderMarketCodes = selectedUser.LeaderMarketCodes
            };

            var taslowToken = _jwtService.IssueToken(auth, expiresAt);
            await WriteAuditAsync(auth, AuthAuditEventTypes.LoginSucceeded, correlationId, cancellationToken);

            return new ProviderLoginCompletion
            {
                AccessToken = taslowToken,
                Session = await BuildContextResponseAsync(auth, cancellationToken),
                ReturnUrl = NormalizeReturnUrl(loginState.ReturnUrl)
            };
        }

        public async Task<AuthContextResponse> ResolveContextAsync(
            IDictionary<string, string> headers,
            bool allowDevHeaders,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            var token = ExtractBearerToken(headers) ?? ExtractCookieToken(headers);
            if (!string.IsNullOrWhiteSpace(token))
            {
                var auth = _jwtService.ValidateToken(token);
                return await BuildContextResponseAsync(auth, cancellationToken);
            }

            var headerAuth = _authorizationService.ResolveAuthContext(headers, allowDevHeaders);
            return await BuildContextResponseAsync(headerAuth, cancellationToken);
        }

        public async Task<LoginOptionsResponse> GetLoginOptionsAsync(CancellationToken cancellationToken = default)
        {
            if (IsProduction())
            {
                throw new TenantApiException(
                    HttpStatusCode.NotFound,
                    AuthErrorCodes.SyntheticNotAllowedInProduction,
                    "Development login options are not available in production.");
            }

            var (items, _) = await _tenantRepository.ListAsync(new TenantListQuery
            {
                Status = TenantStatuses.Active,
                PageSize = 100
            }, cancellationToken);

            return new LoginOptionsResponse
            {
                Items = items.Select(ToLoginOption).ToList()
            };
        }

        public async Task<SelectableUsersResponse> GetSelectableUsersAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            if (IsProduction())
            {
                throw new TenantApiException(
                    HttpStatusCode.NotFound,
                    AuthErrorCodes.SyntheticNotAllowedInProduction,
                    "Development user selection is not available in production.");
            }

            var (tenant, _) = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
            if (tenant == null)
            {
                throw new TenantApiException(HttpStatusCode.NotFound, AuthErrorCodes.TenantNotFound, "Tenant not found.");
            }

            return new SelectableUsersResponse
            {
                Items = (await GetLoginSelectableUsersAsync(tenant, cancellationToken))
                    .Select(user => ToTenantModeUser(user, tenant))
                    .ToList()
            };
        }

        private async Task<IReadOnlyList<SelectableUser>> GetLoginSelectableUsersAsync(
            TenantDocumentDTO tenant,
            CancellationToken cancellationToken)
        {
            var projectUsers = (await _tenantUserCatalog.GetSelectableUsersAsync(
                    tenant.Tenant.TenantId,
                    cancellationToken))
                .ToList();

            var provider = (tenant.Administration.Provider ?? TenantProviders.Microsoft).ToLowerInvariant();
            if (!HasIntegratedProviderBinding(tenant, provider))
            {
                return projectUsers;
            }

            if (provider.Equals(TenantProviders.Microsoft, StringComparison.OrdinalIgnoreCase))
            {
                var projectUsersByEmail = projectUsers
                    .Where(user => !string.IsNullOrWhiteSpace(user.Email))
                    .GroupBy(user => NormalizeEmail(user.Email), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
                var directoryUsers = await TryGetMicrosoftDirectoryUsersAsync(
                    tenant,
                    projectUsersByEmail,
                    cancellationToken);

                if (directoryUsers.Count > 0)
                {
                    return directoryUsers;
                }
            }

            return FilterToInternalUsers(projectUsers, tenant);
        }

        private async Task<List<SelectableUser>> TryGetMicrosoftDirectoryUsersAsync(
            TenantDocumentDTO tenant,
            IReadOnlyDictionary<string, SelectableUser> projectUsersByEmail,
            CancellationToken cancellationToken)
        {
            var configuredFallbackUsers = GetConfiguredIntegratedUsers(tenant, AuthProviders.Microsoft, projectUsersByEmail);
            var microsoftTenantId = tenant.Identity.Microsoft?.MicrosoftTid?.Trim();
            var clientId = GetOptionalConfiguration("Auth:Microsoft:ClientId", "AzureAd:ClientId");
            var clientSecret = GetOptionalConfiguration("Auth:Microsoft:ClientSecret", "AzureAd:ClientSecret");

            if (!HasRealProviderBindingValue(microsoftTenantId)
                || string.IsNullOrWhiteSpace(clientId)
                || string.IsNullOrWhiteSpace(clientSecret))
            {
                return configuredFallbackUsers;
            }

            try
            {
                var accessToken = await RequestMicrosoftGraphTokenAsync(
                    microsoftTenantId!,
                    clientId,
                    clientSecret,
                    cancellationToken);
                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    return configuredFallbackUsers;
                }

                var internalDomains = GetInternalDomains(tenant, AuthProviders.Microsoft);
                var users = await ReadMicrosoftGraphUsersAsync(
                    tenant,
                    accessToken,
                    internalDomains,
                    projectUsersByEmail,
                    cancellationToken);

                return users.Count > 0 ? users : configuredFallbackUsers;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Unable to load Microsoft directory users for tenant {TenantId}; falling back to configured/project-derived internal users.",
                    tenant.Tenant.TenantId);
                return configuredFallbackUsers;
            }
        }

        private async Task<string?> RequestMicrosoftGraphTokenAsync(
            string microsoftTenantId,
            string clientId,
            string clientSecret,
            CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://login.microsoftonline.com/{Uri.EscapeDataString(microsoftTenantId)}/oauth2/v2.0/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["grant_type"] = "client_credentials",
                    ["scope"] = "https://graph.microsoft.com/.default"
                })
            };

            var httpClient = _httpClientFactory.CreateClient();
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Microsoft Graph token request failed for tenant {TenantId}. status={StatusCode}",
                    microsoftTenantId,
                    response.StatusCode);
                return null;
            }

            var payload = JObject.Parse(body);
            return payload.Value<string>("access_token");
        }

        private async Task<List<SelectableUser>> ReadMicrosoftGraphUsersAsync(
            TenantDocumentDTO tenant,
            string accessToken,
            ISet<string> internalDomains,
            IReadOnlyDictionary<string, SelectableUser> projectUsersByEmail,
            CancellationToken cancellationToken)
        {
            var users = new List<SelectableUser>();
            var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var nextUrl = "https://graph.microsoft.com/v1.0/users?$select=id,displayName,mail,userPrincipalName,userType,accountEnabled&$top=100";
            var httpClient = _httpClientFactory.CreateClient();

            while (!string.IsNullOrWhiteSpace(nextUrl))
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, nextUrl);
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {accessToken}");
                using var response = await httpClient.SendAsync(request, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Microsoft Graph users request failed for tenant {TenantId}. status={StatusCode}",
                        tenant.Tenant.TenantId,
                        response.StatusCode);
                    return users;
                }

                var payload = JObject.Parse(body);
                foreach (var graphUser in payload["value"]?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
                {
                    var accountEnabled = graphUser.Value<bool?>("accountEnabled");
                    var userType = graphUser.Value<string>("userType");
                    if (accountEnabled == false
                        || !string.Equals(userType, "Member", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var email = NormalizeEmail(
                        graphUser.Value<string>("mail")
                        ?? graphUser.Value<string>("userPrincipalName")
                        ?? string.Empty);
                    if (string.IsNullOrWhiteSpace(email)
                        || seenEmails.Contains(email)
                        || !IsInternalEmail(email, internalDomains))
                    {
                        continue;
                    }

                    seenEmails.Add(email);
                    users.Add(ToProviderDirectoryUser(
                        tenant,
                        AuthProviders.Microsoft,
                        email,
                        graphUser.Value<string>("displayName"),
                        projectUsersByEmail));
                }

                nextUrl = payload.Value<string>("@odata.nextLink");
            }

            return users
                .OrderBy(user => user.DisplayName)
                .ThenBy(user => user.Email)
                .ToList();
        }

        private List<SelectableUser> GetConfiguredIntegratedUsers(
            TenantDocumentDTO tenant,
            string provider,
            IReadOnlyDictionary<string, SelectableUser> projectUsersByEmail)
        {
            var configuredUsers = ReadConfigurationList(
                $"Auth:IntegratedUsers:{tenant.Tenant.TenantId}",
                $"TenantAuth:IntegratedUsers:{tenant.Tenant.TenantId}",
                $"Auth:{provider}:IntegratedUsers:{tenant.Tenant.TenantId}");

            var users = new List<SelectableUser>();
            var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var internalDomains = GetInternalDomains(tenant, provider);
            foreach (var configuredUser in configuredUsers)
            {
                var (displayName, email) = ParseConfiguredUser(configuredUser);
                if (string.IsNullOrWhiteSpace(email)
                    || seenEmails.Contains(email)
                    || !IsInternalEmail(email, internalDomains))
                {
                    continue;
                }

                seenEmails.Add(email);
                users.Add(ToProviderDirectoryUser(tenant, provider, email, displayName, projectUsersByEmail));
            }

            return users
                .OrderBy(user => user.DisplayName)
                .ThenBy(user => user.Email)
                .ToList();
        }

        private List<SelectableUser> FilterToInternalUsers(
            IEnumerable<SelectableUser> users,
            TenantDocumentDTO tenant)
        {
            var provider = (tenant.Administration.Provider ?? TenantProviders.Microsoft).ToLowerInvariant();
            var internalDomains = GetInternalDomains(tenant, provider);
            if (internalDomains.Count == 0)
            {
                return new List<SelectableUser>();
            }

            return users
                .Where(user => IsInternalEmail(user.Email, internalDomains))
                .OrderBy(user => user.DisplayName)
                .ThenBy(user => user.Email)
                .ToList();
        }

        public async Task RecordLogoutAsync(TenantAuthContext auth, string correlationId, CancellationToken cancellationToken = default)
        {
            await WriteAuditAsync(auth, AuthAuditEventTypes.Logout, correlationId, cancellationToken);
        }

        private async Task<(TenantDocumentDTO Tenant, SelectableUser SelectedUser)> ResolveProviderLoginSelectionAsync(
            string tenantId,
            string selectedUserId,
            CancellationToken cancellationToken)
        {
            var (tenant, _) = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
            if (tenant == null)
            {
                throw new TenantApiException(HttpStatusCode.NotFound, AuthErrorCodes.TenantNotFound, "Tenant not found.");
            }

            if (!tenant.Tenant.Status.Equals(TenantStatuses.Active, StringComparison.OrdinalIgnoreCase))
            {
                throw new TenantApiException(HttpStatusCode.Forbidden, AuthErrorCodes.TenantNotActive, "Tenant is not active.");
            }

            var provider = (tenant.Administration.Provider ?? TenantProviders.Microsoft).ToLowerInvariant();
            if (!HasIntegratedProviderBinding(tenant, provider))
            {
                throw new TenantApiException(
                    HttpStatusCode.Forbidden,
                    AuthErrorCodes.ProviderRequired,
                    "Selected tenant is not configured for integrated provider login.");
            }

            var selectableUsers = await GetLoginSelectableUsersAsync(tenant, cancellationToken);
            var selectedUser = selectableUsers
                .Select(user => ToTenantModeUser(user, tenant))
                .FirstOrDefault(user => IsSelectedUser(user, selectedUserId));

            if (selectedUser == null)
            {
                throw new TenantApiException(
                    HttpStatusCode.Forbidden,
                    AuthErrorCodes.UserNotSelectable,
                    "Selected user is not available for this tenant.");
            }

            if (string.IsNullOrWhiteSpace(selectedUser.Email))
            {
                throw new TenantApiException(
                    HttpStatusCode.Forbidden,
                    AuthErrorCodes.UserNotSelectable,
                    "Selected user must have an email address for provider sign-in.");
            }

            return (tenant, selectedUser);
        }

        private string BuildMicrosoftAuthorizationUrl(
            string microsoftTenantId,
            string clientId,
            string redirectUri,
            string loginHint,
            string state)
        {
            var query = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["response_type"] = "code",
                ["redirect_uri"] = redirectUri,
                ["response_mode"] = "query",
                ["scope"] = _configuration["Auth:Microsoft:Scopes"] ?? "openid profile email User.Read",
                ["state"] = state,
                ["prompt"] = "select_account"
            };

            if (!string.IsNullOrWhiteSpace(loginHint))
            {
                query["login_hint"] = loginHint;
            }

            var queryString = string.Join("&", query.Select(item =>
                $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}"));
            return $"https://login.microsoftonline.com/{Uri.EscapeDataString(microsoftTenantId)}/oauth2/v2.0/authorize?{queryString}";
        }

        private async Task<JObject> ExchangeMicrosoftCodeAsync(
            string microsoftTenantId,
            string code,
            string clientId,
            string clientSecret,
            string redirectUri,
            CancellationToken cancellationToken)
        {
            var tokenEndpoint = $"https://login.microsoftonline.com/{Uri.EscapeDataString(microsoftTenantId)}/oauth2/v2.0/token";
            using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["code"] = code,
                    ["grant_type"] = "authorization_code",
                    ["redirect_uri"] = redirectUri
                })
            };

            var client = _httpClientFactory.CreateClient();
            using var response = await client.SendAsync(request, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new TenantApiException(
                    HttpStatusCode.Unauthorized,
                    AuthErrorCodes.TokenInvalid,
                    "Microsoft authorization code exchange failed.",
                    new[] { raw });
            }

            return JObject.Parse(raw);
        }

        private async Task<ClaimsPrincipal> ValidateMicrosoftIdTokenAsync(
            string idToken,
            string microsoftTenantId,
            string clientId,
            CancellationToken cancellationToken)
        {
            var metadataAddress =
                $"https://login.microsoftonline.com/{Uri.EscapeDataString(microsoftTenantId)}/v2.0/.well-known/openid-configuration";
            var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                metadataAddress,
                new OpenIdConnectConfigurationRetriever());
            var openIdConfiguration = await configurationManager.GetConfigurationAsync(cancellationToken);

            return ProviderTokenHandler.ValidateToken(idToken, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuers = new[]
                {
                    $"https://login.microsoftonline.com/{microsoftTenantId}/v2.0",
                    $"https://sts.windows.net/{microsoftTenantId}/"
                },
                ValidateAudience = true,
                ValidAudience = clientId,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = openIdConfiguration.SigningKeys,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            }, out _);
        }

        private string CreateSignedProviderState(ProviderLoginState state)
        {
            var json = JsonConvert.SerializeObject(state);
            var payload = Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(json));
            var signature = Base64UrlEncoder.Encode(ComputeStateSignature(payload));
            return $"{payload}.{signature}";
        }

        private ProviderLoginState ReadSignedProviderState(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
            {
                throw new TenantApiException(HttpStatusCode.BadRequest, TenantErrorCodes.BadRequest, "Provider login state is required.");
            }

            var parts = state.Split('.', 2);
            if (parts.Length != 2)
            {
                throw new TenantApiException(HttpStatusCode.BadRequest, AuthErrorCodes.TokenInvalid, "Provider login state is malformed.");
            }

            var expectedSignature = ComputeStateSignature(parts[0]);
            var actualSignature = Base64UrlEncoder.DecodeBytes(parts[1]);
            if (actualSignature.Length != expectedSignature.Length
                || !CryptographicOperations.FixedTimeEquals(actualSignature, expectedSignature))
            {
                throw new TenantApiException(HttpStatusCode.BadRequest, AuthErrorCodes.TokenInvalid, "Provider login state signature is invalid.");
            }

            var json = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[0]));
            var loginState = JsonConvert.DeserializeObject<ProviderLoginState>(json);
            if (loginState == null
                || !DateTimeOffset.TryParse(loginState.ExpiresAt, out var expiresAt)
                || expiresAt <= DateTimeOffset.UtcNow)
            {
                throw new TenantApiException(HttpStatusCode.BadRequest, AuthErrorCodes.TokenExpired, "Provider login state is expired.");
            }

            return loginState;
        }

        private byte[] ComputeStateSignature(string payload)
        {
            using var hmac = new HMACSHA256(GetStateSigningKey());
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        }

        private byte[] GetStateSigningKey()
        {
            var secret = _configuration["Auth:ProviderStateSigningKey"]
                ?? _configuration["Auth:JwtSigningKey"]
                ?? "taslow-development-auth-signing-key-change-before-production";
            return Encoding.UTF8.GetBytes(secret);
        }

        private string GetMicrosoftRedirectUri()
            => _configuration["Auth:Microsoft:RedirectUri"]
               ?? "http://localhost:7074/api/auth/callback/microsoft";

        private string GetRequiredConfiguration(params string[] keys)
        {
            foreach (var key in keys)
            {
                var value = _configuration[key];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            throw new TenantApiException(
                HttpStatusCode.InternalServerError,
                AuthErrorCodes.ProviderUnsupported,
                $"Missing required authentication setting: {keys[0]}.");
        }

        private string? GetOptionalConfiguration(params string[] keys)
            => keys
                .Select(key => _configuration[key])
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        private static string NormalizeReturnUrl(string? returnUrl)
        {
            if (string.IsNullOrWhiteSpace(returnUrl)
                || !returnUrl.StartsWith("/", StringComparison.Ordinal)
                || returnUrl.StartsWith("//", StringComparison.Ordinal))
            {
                return "/tasks";
            }

            return returnUrl;
        }

        private static string? FindClaimValue(ClaimsPrincipal principal, params string[] claimTypes)
        {
            var exact = claimTypes
                .Select(type => principal.FindFirst(type)?.Value)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            if (!string.IsNullOrWhiteSpace(exact))
            {
                return exact;
            }

            return principal.Claims
                .Where(claim => claimTypes.Any(type => ClaimTypeMatchesSuffix(claim.Type, type)))
                .Select(claim => claim.Value)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }

        private static bool ClaimTypeMatchesSuffix(string actualType, string requestedType)
        {
            var actual = actualType.Trim();
            var requested = requestedType.Trim();
            return actual.EndsWith($"/{requested}", StringComparison.OrdinalIgnoreCase)
                || actual.EndsWith($":{requested}", StringComparison.OrdinalIgnoreCase);
        }

        private List<string> BuildProviderTenantMismatchDetails(string expectedTenantId, string? actualTenantId)
        {
            if (IsProduction())
            {
                return new List<string>();
            }

            return new List<string>
            {
                $"expectedMicrosoftTenantId={expectedTenantId}",
                $"actualMicrosoftTenantId={actualTenantId ?? "<missing>"}"
            };
        }

        private async Task<AuthContextResponse> BuildContextResponseAsync(
            TenantAuthContext auth,
            CancellationToken cancellationToken)
        {
            var tenantName = "Taslow";
            if (!string.IsNullOrWhiteSpace(auth.TenantId))
            {
                var (tenant, _) = await _tenantRepository.GetByIdAsync(auth.TenantId, cancellationToken);
                tenantName = tenant?.Tenant.DisplayName ?? auth.TenantId!;
            }

            return new AuthContextResponse
            {
                Authenticated = true,
                Environment = auth.Environment,
                TenantId = auth.TenantId ?? string.Empty,
                TenantName = tenantName,
                Provider = auth.Provider,
                IdentityMode = auth.IdentityMode,
                Roles = auth.Roles.Count > 0 ? auth.Roles : new List<string> { auth.Role },
                Permissions = auth.Permissions,
                LeaderMarketCodes = auth.LeaderMarketCodes,
                ExpiresAt = auth.ExpiresAt ?? string.Empty,
                User = new AuthUserContext
                {
                    Subject = auth.Subject,
                    DisplayName = auth.DisplayName,
                    Email = auth.Email,
                    ProviderTenantId = auth.ProviderTenantId
                }
            };
        }

        private async Task WriteAuditAsync(
            TenantAuthContext auth,
            string eventType,
            string correlationId,
            CancellationToken cancellationToken)
        {
            try
            {
                await _auditRepository.CreateAsync(new AuthenticationAuditRecord
                {
                    Id = Guid.NewGuid().ToString(),
                    Jti = auth.Jti ?? string.Empty,
                    Environment = auth.Environment,
                    TenantId = auth.TenantId ?? "taslow",
                    Provider = auth.Provider,
                    IdentityMode = auth.IdentityMode,
                    Subject = auth.Subject,
                    Roles = auth.Roles.Count > 0 ? auth.Roles : new List<string> { auth.Role },
                    IssuedAt = DateTime.UtcNow.ToString("O"),
                    ExpiresAt = auth.ExpiresAt,
                    Impersonated = auth.Impersonated,
                    EventType = eventType,
                    CorrelationId = correlationId,
                    CreatedAt = DateTime.UtcNow.ToString("O")
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write authentication audit event {EventType}.", eventType);
            }
        }

        private TenantLoginOption ToLoginOption(TenantDocumentDTO tenant)
        {
            var provider = (tenant.Administration.Provider ?? TenantProviders.Microsoft).ToLowerInvariant();
            var identityMode = HasIntegratedProviderBinding(tenant, provider)
                ? IdentityModes.Integrated
                : IdentityModes.Synthetic;

            return new TenantLoginOption
            {
                TenantId = tenant.Tenant.TenantId,
                DisplayName = tenant.Tenant.DisplayName,
                Status = tenant.Tenant.Status,
                Provider = identityMode == IdentityModes.Synthetic ? AuthProviders.Synthetic : provider,
                IdentityMode = identityMode,
                LoginEnabled = tenant.Tenant.Status.Equals(TenantStatuses.Active, StringComparison.OrdinalIgnoreCase)
            };
        }

        private static bool HasIntegratedProviderBinding(TenantDocumentDTO tenant, string provider)
        {
            if (provider.Equals(TenantProviders.Microsoft, StringComparison.OrdinalIgnoreCase))
            {
                return HasRealProviderBindingValue(tenant.Identity.Microsoft?.MicrosoftTid);
            }

            if (provider.Equals(TenantProviders.Google, StringComparison.OrdinalIgnoreCase))
            {
                return HasRealProviderBindingValue(tenant.Identity.Google?.WorkspaceCustomerId)
                    || HasRealProviderBindingValue(tenant.Identity.Google?.HostedDomainHd);
            }

            return false;
        }

        private static SelectableUser ToTenantModeUser(SelectableUser user, TenantDocumentDTO tenant)
        {
            var provider = (tenant.Administration.Provider ?? TenantProviders.Microsoft).ToLowerInvariant();
            if (!HasIntegratedProviderBinding(tenant, provider))
            {
                return user;
            }

            user.Provider = provider;
            user.IdentityMode = IdentityModes.Integrated;
            user.Subject = $"{provider}:{tenant.Tenant.TenantId}:{NormalizeSubjectPart(user.Email)}";
            user.TenantUserId = user.Subject;
            user.Source = string.IsNullOrWhiteSpace(user.Source)
                ? "project_directory_projection"
                : $"{user.Source},provider_login_required";
            user.RoleDerivationSummary = string.IsNullOrWhiteSpace(user.RoleDerivationSummary)
                ? "Derived from project relationships; provider login required."
                : $"{user.RoleDerivationSummary} Provider login required.";

            return user;
        }

        private SelectableUser ToProviderDirectoryUser(
            TenantDocumentDTO tenant,
            string provider,
            string email,
            string? displayName,
            IReadOnlyDictionary<string, SelectableUser> projectUsersByEmail)
        {
            email = NormalizeEmail(email);
            projectUsersByEmail.TryGetValue(email, out var projectUser);

            var roles = projectUser?.Roles
                .Where(role => TenantRoles.All.Contains(role))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
                ?? new List<string>();
            if (roles.Count == 0)
            {
                roles.Add(TenantRoles.TenantUser);
            }

            var primaryRole = roles.Contains(TenantRoles.TenantPm, StringComparer.OrdinalIgnoreCase)
                ? TenantRoles.TenantPm
                : roles[0];
            var subject = $"{provider}:{tenant.Tenant.TenantId}:{NormalizeSubjectPart(email)}";
            var source = string.IsNullOrWhiteSpace(projectUser?.Source)
                ? "provider_directory"
                : $"{projectUser!.Source},provider_directory";

            return new SelectableUser
            {
                TenantUserId = subject,
                Subject = subject,
                DisplayName = string.IsNullOrWhiteSpace(displayName)
                    ? projectUser?.DisplayName ?? BuildDisplayName(email)
                    : displayName.Trim(),
                Email = email,
                Source = source,
                Provider = provider,
                IdentityMode = IdentityModes.Integrated,
                PrimaryRole = primaryRole,
                Roles = roles,
                RoleDerivationSummary = projectUser == null
                    ? "Loaded from provider directory; default tenant user role."
                    : $"{projectUser.RoleDerivationSummary} Loaded from provider directory."
            };
        }

        private HashSet<string> GetInternalDomains(TenantDocumentDTO tenant, string provider)
        {
            var domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (provider.Equals(TenantProviders.Microsoft, StringComparison.OrdinalIgnoreCase))
            {
                AddDomains(domains, tenant.Identity.Microsoft?.AllowedDomains);
            }
            else if (provider.Equals(TenantProviders.Google, StringComparison.OrdinalIgnoreCase))
            {
                AddDomains(domains, tenant.Identity.Google?.AllowedDomains);
                AddDomain(domains, tenant.Identity.Google?.HostedDomainHd);
            }

            AddDomain(domains, ExtractEmailDomain(tenant.Tenant.CompanyPocEmail));
            AddDomains(domains, ReadConfigurationList(
                $"Auth:IntegratedInternalDomains:{tenant.Tenant.TenantId}",
                $"TenantAuth:IntegratedInternalDomains:{tenant.Tenant.TenantId}",
                $"Auth:{provider}:IntegratedInternalDomains:{tenant.Tenant.TenantId}"));

            return domains;
        }

        private List<string> ReadConfigurationList(params string[] keys)
        {
            return keys
                .Select(key => _configuration[key])
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .SelectMany(value => value!.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static (string? DisplayName, string Email) ParseConfiguredUser(string value)
        {
            var trimmed = value.Trim();
            var bracketStart = trimmed.IndexOf('<');
            var bracketEnd = trimmed.IndexOf('>');
            if (bracketStart >= 0 && bracketEnd > bracketStart)
            {
                return (
                    trimmed[..bracketStart].Trim(),
                    NormalizeEmail(trimmed[(bracketStart + 1)..bracketEnd]));
            }

            var pipeParts = trimmed.Split('|', 2, StringSplitOptions.TrimEntries);
            if (pipeParts.Length == 2)
            {
                return (pipeParts[0], NormalizeEmail(pipeParts[1]));
            }

            return (null, NormalizeEmail(trimmed));
        }

        private static bool IsInternalEmail(string email, ISet<string> internalDomains)
        {
            if (internalDomains.Count == 0)
            {
                return true;
            }

            var domain = ExtractEmailDomain(email);
            return !string.IsNullOrWhiteSpace(domain) && internalDomains.Contains(domain);
        }

        private static void AddDomains(ISet<string> domains, IEnumerable<string>? values)
        {
            if (values == null)
            {
                return;
            }

            foreach (var value in values)
            {
                AddDomain(domains, value);
            }
        }

        private static void AddDomain(ISet<string> domains, string? value)
        {
            var domain = NormalizeDomain(value);
            if (!string.IsNullOrWhiteSpace(domain))
            {
                domains.Add(domain);
            }
        }

        private static string NormalizeDomain(string? value)
        {
            var trimmed = value?.Trim().TrimStart('@').ToLowerInvariant();
            return string.IsNullOrWhiteSpace(trimmed) ? string.Empty : trimmed;
        }

        private static string ExtractEmailDomain(string? email)
        {
            var normalized = NormalizeEmail(email ?? string.Empty);
            var atIndex = normalized.LastIndexOf('@');
            return atIndex < 0 || atIndex == normalized.Length - 1
                ? string.Empty
                : normalized[(atIndex + 1)..];
        }

        private static string NormalizeEmail(string value)
            => value.Trim().ToLowerInvariant();

        private static string BuildDisplayName(string email)
        {
            var local = email.Split('@')[0];
            var parts = local.Split(new[] { '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0
                ? email
                : string.Join(" ", parts.Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        }

        private static bool IsSyntheticTenant(TenantDocumentDTO tenant)
        {
            var provider = (tenant.Administration.Provider ?? TenantProviders.Microsoft).ToLowerInvariant();
            return !HasIntegratedProviderBinding(tenant, provider);
        }

        private static bool HasRealProviderBindingValue(string? value)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)
                || trimmed.Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var significantChars = trimmed.Where(char.IsLetterOrDigit).ToArray();
            return significantChars.Length > 0 && significantChars.Any(ch => ch != '0');
        }

        private static bool IsSelectedUser(SelectableUser user, string selectedUserId)
        {
            var normalized = selectedUserId.Trim();
            return normalized.Equals(user.TenantUserId, StringComparison.OrdinalIgnoreCase)
                || normalized.Equals(user.Subject, StringComparison.OrdinalIgnoreCase)
                || normalized.Equals(user.Email, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeSubjectPart(string value)
            => value.Trim().ToLowerInvariant().Replace("@", "_at_").Replace(".", "_");

        private static string? ExtractBearerToken(IDictionary<string, string> headers)
        {
            if (!headers.TryGetValue("Authorization", out var authorization)
                || string.IsNullOrWhiteSpace(authorization))
            {
                return null;
            }

            const string prefix = "Bearer ";
            return authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? authorization[prefix.Length..].Trim()
                : null;
        }

        private string? ExtractCookieToken(IDictionary<string, string> headers)
        {
            if (!headers.TryGetValue("Cookie", out var cookieHeader)
                || string.IsNullOrWhiteSpace(cookieHeader))
            {
                return null;
            }

            var cookieName = _configuration["Auth:CookieName"] ?? "taslow_auth";
            var cookies = cookieHeader.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var cookie in cookies)
            {
                var pair = cookie.Split('=', 2);
                if (pair.Length == 2 && pair[0].Trim().Equals(cookieName, StringComparison.OrdinalIgnoreCase))
                {
                    return pair[1].Trim();
                }
            }

            return null;
        }

        private bool IsSyntheticLoginEnabled()
        {
            var raw = _configuration["Auth:EnableSyntheticLogin"];
            return string.IsNullOrWhiteSpace(raw)
                ? !IsProduction()
                : bool.TryParse(raw, out var enabled) && enabled;
        }

        private bool IsProduction()
            => EnvironmentName.Equals(TaslowEnvironments.Production, StringComparison.OrdinalIgnoreCase);

        private string EnvironmentName
            => (_configuration["Auth:Environment"] ?? TaslowEnvironments.Development).ToLowerInvariant();

        private int GetSessionLifetimeMinutes()
        {
            var raw = _configuration["Auth:SessionLifetimeMinutes"];
            if (int.TryParse(raw, out var minutes) && minutes > 0)
            {
                return minutes;
            }

            return IsProduction() ? 15 : 480;
        }

        private sealed class ProviderLoginState
        {
            public string TenantId { get; set; } = string.Empty;
            public string Provider { get; set; } = AuthProviders.Microsoft;
            public string SelectedUserId { get; set; } = string.Empty;
            public string LoginHint { get; set; } = string.Empty;
            public string ReturnUrl { get; set; } = "/tasks";
            public string ExpiresAt { get; set; } = string.Empty;
            public string CorrelationId { get; set; } = string.Empty;
            public string Nonce { get; set; } = string.Empty;
        }
    }
}
