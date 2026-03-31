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
    public class TenantFunction
    {
        private readonly ITenantService _tenantService;
        private readonly ITenantAuthorizationService _authorizationService;
        private readonly ILogger<TenantFunction> _logger;
        private readonly IConfiguration _configuration;

        public TenantFunction(
            ITenantService tenantService,
            ITenantAuthorizationService authorizationService,
            ILogger<TenantFunction> logger,
            IConfiguration configuration)
        {
            _tenantService = tenantService;
            _authorizationService = authorizationService;
            _logger = logger;
            _configuration = configuration;
        }

        [Function("GetTenants")]
        public Task<HttpResponseData> GetTenants(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "tenants")] HttpRequestData req)
            => ExecuteAsync(req, async (auth, correlationId) =>
            {
                var query = ParseListQuery(req);
                var result = await _tenantService.ListAsync(query, auth, req.FunctionContext.CancellationToken);
                return await Json(req, HttpStatusCode.OK, result, correlationId);
            });

        [Function("GetTenantById")]
        public Task<HttpResponseData> GetTenantById(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "tenants/{tenantId}")] HttpRequestData req,
            string tenantId)
            => ExecuteAsync(req, async (auth, correlationId) =>
            {
                var result = await _tenantService.GetByIdAsync(tenantId, auth, req.FunctionContext.CancellationToken);
                return await Json(req, HttpStatusCode.OK, result, correlationId);
            });

        [Function("CreateTenant")]
        public Task<HttpResponseData> CreateTenant(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "tenants")] HttpRequestData req)
            => ExecuteAsync(req, async (auth, correlationId) =>
            {
                var request = await ReadBodyAsync<TenantCreateRequest>(req);
                var result = await _tenantService.CreateAsync(request, auth, req.FunctionContext.CancellationToken);
                return await Json(req, HttpStatusCode.OK, result, correlationId);
            });

        [Function("PatchTenantSection")]
        public Task<HttpResponseData> PatchTenantSection(
            [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "tenants/{tenantId}/tenant")] HttpRequestData req,
            string tenantId)
            => ExecuteAsync(req, async (auth, correlationId) =>
            {
                var request = await ReadBodyAsync<TenantDetailsPatchRequest>(req);
                var ifMatch = GetHeader(req, "If-Match");
                var result = await _tenantService.PatchTenantAsync(tenantId, request, ifMatch, auth, req.FunctionContext.CancellationToken);
                return await Json(req, HttpStatusCode.OK, result, correlationId);
            });

        [Function("PatchTenantBilling")]
        public Task<HttpResponseData> PatchTenantBilling(
            [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "tenants/{tenantId}/billing")] HttpRequestData req,
            string tenantId)
            => ExecuteAsync(req, async (auth, correlationId) =>
            {
                var request = await ReadBodyAsync<TenantBillingPatchRequest>(req);
                var ifMatch = GetHeader(req, "If-Match");
                var result = await _tenantService.PatchBillingAsync(tenantId, request, ifMatch, auth, req.FunctionContext.CancellationToken);
                return await Json(req, HttpStatusCode.OK, result, correlationId);
            });

        [Function("PatchTenantAdministration")]
        public Task<HttpResponseData> PatchTenantAdministration(
            [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "tenants/{tenantId}/administration")] HttpRequestData req,
            string tenantId)
            => ExecuteAsync(req, async (auth, correlationId) =>
            {
                var request = await ReadBodyAsync<TenantAdministrationPatchRequest>(req);
                var ifMatch = GetHeader(req, "If-Match");
                var result = await _tenantService.PatchAdministrationAsync(tenantId, request, ifMatch, auth, req.FunctionContext.CancellationToken);
                return await Json(req, HttpStatusCode.OK, result, correlationId);
            });

        [Function("PatchTenantIdentity")]
        public Task<HttpResponseData> PatchTenantIdentity(
            [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "tenants/{tenantId}/identity")] HttpRequestData req,
            string tenantId)
            => ExecuteAsync(req, async (auth, correlationId) =>
            {
                var request = await ReadBodyAsync<TenantIdentityPatchRequest>(req);
                var ifMatch = GetHeader(req, "If-Match");
                var result = await _tenantService.PatchIdentityAsync(tenantId, request, ifMatch, auth, req.FunctionContext.CancellationToken);
                return await Json(req, HttpStatusCode.OK, result, correlationId);
            });

        [Function("PatchTenantEmailIntegration")]
        public Task<HttpResponseData> PatchTenantEmailIntegration(
            [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "tenants/{tenantId}/email-integration")] HttpRequestData req,
            string tenantId)
            => ExecuteAsync(req, async (auth, correlationId) =>
            {
                var request = await ReadBodyAsync<TenantEmailIntegrationPatchRequest>(req);
                var ifMatch = GetHeader(req, "If-Match");
                var result = await _tenantService.PatchEmailIntegrationAsync(tenantId, request, ifMatch, auth, req.FunctionContext.CancellationToken);
                return await Json(req, HttpStatusCode.OK, result, correlationId);
            });

        private async Task<HttpResponseData> ExecuteAsync(
            HttpRequestData req,
            Func<TenantAuthContext, string, Task<HttpResponseData>> operation)
        {
            var correlationId = GetCorrelationId(req);
            var allowDevHeaders = IsDevHeadersEnabled();

            try
            {
                var auth = _authorizationService.ResolveAuthContext(ToDictionary(req.Headers), allowDevHeaders);
                return await operation(auth, correlationId);
            }
            catch (TenantApiException ex)
            {
                _logger.LogWarning(ex, "Tenant API error: {Code} - {Message}", ex.Code, ex.Message);
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
                _logger.LogError(ex, "Unhandled tenant API error.");
                var payload = new ApiErrorResponse
                {
                    Error = new ApiError
                    {
                        Code = TenantErrorCodes.BadRequest,
                        Message = "Unhandled server error.",
                        CorrelationId = correlationId,
                        Details = new List<string>()
                    }
                };

                return await Json(req, HttpStatusCode.InternalServerError, payload, correlationId);
            }
        }

        private static async Task<T> ReadBodyAsync<T>(HttpRequestData req) where T : class, new()
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(body))
            {
                return new T();
            }

            return JsonConvert.DeserializeObject<T>(body) ?? new T();
        }

        private static async Task<HttpResponseData> Json<T>(HttpRequestData req, HttpStatusCode statusCode, T payload, string correlationId)
        {
            var response = req.CreateResponse(statusCode);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            response.Headers.Add("x-correlation-id", correlationId);
            var json = JsonConvert.SerializeObject(payload);
            await response.WriteStringAsync(json, Encoding.UTF8);
            return response;
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

        private bool IsDevHeadersEnabled()
        {
            var value = _configuration["TenantAuth:EnableDevHeaders"];
            return bool.TryParse(value, out var enabled) && enabled;
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

        private static TenantListQuery ParseListQuery(HttpRequestData req)
        {
            var map = ParseQuery(req.Url.Query);
            var pageSize = 25;
            if (map.TryGetValue("pageSize", out var rawPageSize))
            {
                _ = int.TryParse(rawPageSize, out pageSize);
            }

            map.TryGetValue("status", out var status);
            map.TryGetValue("search", out var search);
            map.TryGetValue("continuationToken", out var continuationToken);

            return new TenantListQuery
            {
                Status = status,
                Search = search,
                ContinuationToken = continuationToken,
                PageSize = pageSize
            };
        }

        private static Dictionary<string, string> ParseQuery(string queryString)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(queryString))
            {
                return result;
            }

            var trimmed = queryString.TrimStart('?');
            var parts = trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 0)
                {
                    continue;
                }

                var key = Uri.UnescapeDataString(kv[0]);
                var value = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
                result[key] = value;
            }

            return result;
        }
    }
}
