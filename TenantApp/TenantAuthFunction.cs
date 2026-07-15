using System.Net;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Taslow.Shared.Model;
using Taslow.Tenant.Model;
using Taslow.Tenant.Service.Interface;

namespace Taslow.Tenant.Function
{
    public class TenantAuthFunction
    {
        private readonly ITenantAuthService _authService;
        private readonly ITaslowJwtService _jwtService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TenantAuthFunction> _logger;

        public TenantAuthFunction(
            ITenantAuthService authService,
            ITaslowJwtService jwtService,
            IConfiguration configuration,
            ILogger<TenantAuthFunction> logger)
        {
            _authService = authService;
            _jwtService = jwtService;
            _configuration = configuration;
            _logger = logger;
        }

        [Function("GetAuthContext")]
        public Task<HttpResponseData> GetAuthContext(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/context")] HttpRequestData req)
            => ExecuteAsync(req, async correlationId =>
            {
                var context = await _authService.ResolveContextAsync(
                    ToDictionary(req.Headers),
                    IsDevHeadersEnabled(),
                    correlationId,
                    req.FunctionContext.CancellationToken);

                return await Json(req, HttpStatusCode.OK, context, correlationId);
            });

        [Function("GetLoginOptions")]
        public Task<HttpResponseData> GetLoginOptions(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/login-options")] HttpRequestData req)
            => ExecuteAsync(req, async correlationId =>
            {
                var result = await _authService.GetLoginOptionsAsync(req.FunctionContext.CancellationToken);
                return await Json(req, HttpStatusCode.OK, result, correlationId);
            });

        [Function("GetSelectableUsers")]
        public Task<HttpResponseData> GetSelectableUsers(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/tenants/{tenantId}/users")] HttpRequestData req,
            string tenantId)
            => ExecuteAsync(req, async correlationId =>
            {
                var result = await _authService.GetSelectableUsersAsync(tenantId, req.FunctionContext.CancellationToken);
                return await Json(req, HttpStatusCode.OK, result, correlationId);
            });

        [Function("CreateDevSession")]
        public Task<HttpResponseData> CreateDevSession(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/dev/session")] HttpRequestData req)
            => ExecuteAsync(req, async correlationId =>
            {
                var request = await ReadBodyAsync<DevSessionRequest>(req);
                var result = await _authService.CreateDevSessionAsync(
                    request,
                    correlationId,
                    req.FunctionContext.CancellationToken);

                var response = await Json(req, HttpStatusCode.OK, result, correlationId);
                AddAuthCookie(response, result.AccessToken);
                return response;
            });

        [Function("StartProviderLogin")]
        public Task<HttpResponseData> StartProviderLogin(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/provider/start")] HttpRequestData req)
            => ExecuteAsync(req, async correlationId =>
            {
                var request = await ReadBodyAsync<ProviderLoginStartRequest>(req);
                var result = await _authService.StartProviderLoginAsync(
                    request,
                    correlationId,
                    req.FunctionContext.CancellationToken);

                return await Json(req, HttpStatusCode.OK, result, correlationId);
            });

        [Function("MicrosoftAuthCallback")]
        public Task<HttpResponseData> MicrosoftAuthCallback(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/callback/microsoft")] HttpRequestData req)
            => ExecuteAsync(req, async correlationId =>
            {
                var query = ParseQuery(req.Url);
                if (query.TryGetValue("error", out var providerError)
                    && !string.IsNullOrWhiteSpace(providerError))
                {
                    var description = query.TryGetValue("error_description", out var value)
                        ? value
                        : "Microsoft sign-in was not completed.";
                    throw new TenantApiException(
                        HttpStatusCode.Unauthorized,
                        AuthErrorCodes.TokenInvalid,
                        description);
                }

                query.TryGetValue("code", out var code);
                query.TryGetValue("state", out var state);
                var result = await _authService.CompleteMicrosoftLoginAsync(
                    code ?? string.Empty,
                    state ?? string.Empty,
                    correlationId,
                    req.FunctionContext.CancellationToken);

                var response = req.CreateResponse(HttpStatusCode.Found);
                response.Headers.Add("Location", BuildWebAppRedirectUrl(result.ReturnUrl));
                response.Headers.Add("x-correlation-id", correlationId);
                AddAuthCookie(response, result.AccessToken);
                return response;
            });

        [Function("Logout")]
        public Task<HttpResponseData> Logout(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/logout")] HttpRequestData req)
            => ExecuteAsync(req, async correlationId =>
            {
                var token = ExtractBearerToken(ToDictionary(req.Headers)) ?? ExtractCookieToken(ToDictionary(req.Headers));
                if (!string.IsNullOrWhiteSpace(token))
                {
                    var auth = _jwtService.ValidateToken(token);
                    await _authService.RecordLogoutAsync(auth, correlationId, req.FunctionContext.CancellationToken);
                }

                var response = await Json(req, HttpStatusCode.OK, new { loggedOut = true }, correlationId);
                ClearAuthCookie(response);
                return response;
            });

        private async Task<HttpResponseData> ExecuteAsync(
            HttpRequestData req,
            Func<string, Task<HttpResponseData>> operation)
        {
            var correlationId = GetCorrelationId(req);
            try
            {
                return await operation(correlationId);
            }
            catch (TenantApiException ex)
            {
                _logger.LogWarning(ex, "Tenant Auth API error: {Code} - {Message}", ex.Code, ex.Message);
                var payload = new ApiErrorResponse
                {
                    Error = new ApiError
                    {
                        Code = ex.Code,
                        Message = ex.Message,
                        CorrelationId = correlationId,
                        Details = ex.Details
                    }
                };

                return await Json(req, ex.StatusCode, payload, correlationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled tenant auth API error.");
                var payload = new ApiErrorResponse
                {
                    Error = new ApiError
                    {
                        Code = TenantErrorCodes.BadRequest,
                        Message = "Unhandled auth server error.",
                        CorrelationId = correlationId
                    }
                };

                return await Json(req, HttpStatusCode.InternalServerError, payload, correlationId);
            }
        }

        private static async Task<T> ReadBodyAsync<T>(HttpRequestData req) where T : class, new()
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            return string.IsNullOrWhiteSpace(body)
                ? new T()
                : JsonConvert.DeserializeObject<T>(body) ?? new T();
        }

        private static async Task<HttpResponseData> Json<T>(HttpRequestData req, HttpStatusCode statusCode, T payload, string correlationId)
        {
            var response = req.CreateResponse(statusCode);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            response.Headers.Add("x-correlation-id", correlationId);
            await response.WriteStringAsync(JsonConvert.SerializeObject(payload), Encoding.UTF8);
            return response;
        }

        private void AddAuthCookie(HttpResponseData response, string token)
        {
            var cookieName = _configuration["Auth:CookieName"] ?? "taslow_auth";
            var maxAgeSeconds = GetSessionLifetimeMinutes() * 60;
            var secure = IsProduction() || IsCookieSecureEnabled();
            var secureFlag = secure ? "; Secure" : string.Empty;
            response.Headers.Add(
                "Set-Cookie",
                $"{cookieName}={token}; HttpOnly; SameSite=Lax; Path=/; Max-Age={maxAgeSeconds}{secureFlag}");
        }

        private void ClearAuthCookie(HttpResponseData response)
        {
            var cookieName = _configuration["Auth:CookieName"] ?? "taslow_auth";
            var secure = IsProduction() || IsCookieSecureEnabled();
            var secureFlag = secure ? "; Secure" : string.Empty;
            response.Headers.Add(
                "Set-Cookie",
                $"{cookieName}=; HttpOnly; SameSite=Lax; Path=/; Max-Age=0{secureFlag}");
        }

        private bool IsDevHeadersEnabled()
        {
            var value = _configuration["TenantAuth:EnableDevHeaders"];
            return bool.TryParse(value, out var enabled) && enabled;
        }

        private bool IsProduction()
        {
            var environment = _configuration["Auth:Environment"] ?? TaslowEnvironments.Development;
            return environment.Equals(TaslowEnvironments.Production, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsCookieSecureEnabled()
        {
            var value = _configuration["Auth:CookieSecure"];
            return bool.TryParse(value, out var enabled) && enabled;
        }

        private int GetSessionLifetimeMinutes()
        {
            var raw = _configuration["Auth:SessionLifetimeMinutes"];
            return int.TryParse(raw, out var minutes) && minutes > 0
                ? minutes
                : IsProduction() ? 15 : 480;
        }

        private static string GetHeader(HttpRequestData req, string key)
        {
            if (req.Headers.TryGetValues(key, out var values))
            {
                return values.FirstOrDefault() ?? string.Empty;
            }

            return string.Empty;
        }

        private static string GetCorrelationId(HttpRequestData req)
        {
            var incoming = GetHeader(req, "x-correlation-id");
            return string.IsNullOrWhiteSpace(incoming) ? Guid.NewGuid().ToString() : incoming;
        }

        private string BuildWebAppRedirectUrl(string returnUrl)
        {
            var baseUrl = _configuration["Auth:WebAppBaseUrl"] ?? "http://localhost:5173";
            var safeReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
                || !returnUrl.StartsWith("/", StringComparison.Ordinal)
                || returnUrl.StartsWith("//", StringComparison.Ordinal)
                    ? "/tasks"
                    : returnUrl;

            return new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), safeReturnUrl.TrimStart('/')).ToString();
        }

        private static Dictionary<string, string> ParseQuery(Uri url)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var query = url.Query;
            if (string.IsNullOrWhiteSpace(query))
            {
                return result;
            }

            foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split('=', 2);
                var key = Uri.UnescapeDataString(pair[0].Replace("+", " "));
                var value = pair.Length == 2
                    ? Uri.UnescapeDataString(pair[1].Replace("+", " "))
                    : string.Empty;
                result[key] = value;
            }

            return result;
        }

        private static Dictionary<string, string> ToDictionary(HttpHeadersCollection headers)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in headers)
            {
                dict[header.Key] = header.Value.FirstOrDefault() ?? string.Empty;
            }

            return dict;
        }

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
            foreach (var cookie in cookieHeader.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = cookie.Split('=', 2);
                if (pair.Length == 2 && pair[0].Trim().Equals(cookieName, StringComparison.OrdinalIgnoreCase))
                {
                    return pair[1].Trim();
                }
            }

            return null;
        }
    }
}
