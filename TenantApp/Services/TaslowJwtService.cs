using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Taslow.Shared.Model;
using Taslow.Tenant.Model;
using Taslow.Tenant.Service.Interface;

namespace Taslow.Tenant.Service
{
    public class TaslowJwtService : ITaslowJwtService
    {
        private readonly IConfiguration _configuration;
        private readonly JwtSecurityTokenHandler _handler = new();

        public TaslowJwtService(IConfiguration configuration)
        {
            _configuration = configuration;
            _handler.MapInboundClaims = false;
        }

        public string IssueToken(TenantAuthContext auth, DateTimeOffset expiresAt)
        {
            var now = DateTimeOffset.UtcNow;
            var jti = string.IsNullOrWhiteSpace(auth.Jti) ? Guid.NewGuid().ToString() : auth.Jti!;
            var roles = auth.Roles.Count > 0 ? auth.Roles : new List<string> { auth.Role };
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, auth.Subject),
                new(JwtRegisteredClaimNames.Jti, jti),
                new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
                new(TaslowClaimTypes.TenantId, auth.TenantId ?? string.Empty),
                new(TaslowClaimTypes.Provider, auth.Provider),
                new(TaslowClaimTypes.IdentityMode, auth.IdentityMode),
                new(TaslowClaimTypes.Environment, auth.Environment),
                new(TaslowClaimTypes.Impersonated, auth.Impersonated ? "true" : "false")
            };

            if (!string.IsNullOrWhiteSpace(auth.ProviderTenantId))
            {
                claims.Add(new Claim(TaslowClaimTypes.ProviderTenantId, auth.ProviderTenantId!));
            }

            if (!string.IsNullOrWhiteSpace(auth.ProviderSubject))
            {
                claims.Add(new Claim(TaslowClaimTypes.ProviderSubject, auth.ProviderSubject!));
            }

            if (!string.IsNullOrWhiteSpace(auth.Email))
            {
                claims.Add(new Claim(ClaimTypes.Email, auth.Email));
            }

            if (!string.IsNullOrWhiteSpace(auth.DisplayName))
            {
                claims.Add(new Claim(ClaimTypes.Name, auth.DisplayName));
            }

            claims.AddRange(roles.Select(role => new Claim(TaslowClaimTypes.Roles, role)));
            claims.AddRange(auth.Permissions.Select(permission => new Claim(TaslowClaimTypes.Permissions, permission)));
            claims.AddRange(auth.LeaderMarketCodes.Select(code => new Claim(TaslowClaimTypes.LeaderMarketCodes, code)));

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = Issuer,
                Audience = Audience,
                Subject = new ClaimsIdentity(claims),
                NotBefore = now.UtcDateTime,
                Expires = expiresAt.UtcDateTime,
                SigningCredentials = new SigningCredentials(GetSigningKey(), SecurityAlgorithms.HmacSha256)
            };

            var token = _handler.CreateToken(descriptor);
            return _handler.WriteToken(token);
        }

        public TenantAuthContext ValidateToken(string token)
        {
            try
            {
                var principal = _handler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = Issuer,
                    ValidateAudience = true,
                    ValidAudience = Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = GetSigningKey(),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2)
                }, out var securityToken);

                if (securityToken is not JwtSecurityToken jwt
                    || !jwt.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new TenantApiException(
                        System.Net.HttpStatusCode.Unauthorized,
                        AuthErrorCodes.TokenInvalid,
                        "Taslow token signature algorithm is invalid.");
                }

                var roles = FindClaimValues(
                        principal,
                        TaslowClaimTypes.Roles,
                        ClaimTypes.Role,
                        "role",
                        "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (roles.Count == 0)
                {
                    throw new TenantApiException(
                        System.Net.HttpStatusCode.Unauthorized,
                        AuthErrorCodes.TokenInvalid,
                        "Taslow token is missing role claims.");
                }

                return new TenantAuthContext
                {
                    Role = roles.Contains(TenantRoles.TaslowAdmin, StringComparer.OrdinalIgnoreCase)
                        ? TenantRoles.TaslowAdmin
                        : roles[0].ToLowerInvariant(),
                    TenantId = FindClaimValue(principal, TaslowClaimTypes.TenantId),
                    Subject = FindClaimValue(principal, JwtRegisteredClaimNames.Sub, ClaimTypes.NameIdentifier) ?? string.Empty,
                    DisplayName = FindClaimValue(principal, ClaimTypes.Name, JwtRegisteredClaimNames.Name, "name") ?? string.Empty,
                    Email = FindClaimValue(principal, ClaimTypes.Email, JwtRegisteredClaimNames.Email, "email") ?? string.Empty,
                    Provider = FindClaimValue(principal, TaslowClaimTypes.Provider) ?? AuthProviders.Synthetic,
                    IdentityMode = FindClaimValue(principal, TaslowClaimTypes.IdentityMode) ?? IdentityModes.Synthetic,
                    Environment = FindClaimValue(principal, TaslowClaimTypes.Environment) ?? TaslowEnvironments.Development,
                    ProviderTenantId = FindClaimValue(principal, TaslowClaimTypes.ProviderTenantId),
                    ProviderSubject = FindClaimValue(principal, TaslowClaimTypes.ProviderSubject),
                    Jti = FindClaimValue(principal, JwtRegisteredClaimNames.Jti),
                    ExpiresAt = jwt.ValidTo.ToString("O"),
                    Impersonated = string.Equals(
                        FindClaimValue(principal, TaslowClaimTypes.Impersonated),
                        "true",
                        StringComparison.OrdinalIgnoreCase),
                    Roles = roles,
                    Permissions = FindClaimValues(principal, TaslowClaimTypes.Permissions)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    LeaderMarketCodes = FindClaimValues(principal, TaslowClaimTypes.LeaderMarketCodes)
                        .Select(code => code.ToUpperInvariant())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
                };
            }
            catch (SecurityTokenExpiredException ex)
            {
                throw new TenantApiException(
                    System.Net.HttpStatusCode.Unauthorized,
                    AuthErrorCodes.TokenExpired,
                    "Taslow token is expired.",
                    new[] { ex.Message });
            }
            catch (TenantApiException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new TenantApiException(
                    System.Net.HttpStatusCode.Unauthorized,
                    AuthErrorCodes.TokenInvalid,
                    "Taslow token is invalid.",
                    new[] { ex.Message });
            }
        }

        private string Issuer => _configuration["Auth:Issuer"] ?? "taslow-dev-auth";

        private string Audience => _configuration["Auth:Audience"] ?? "taslow-api";

        private SymmetricSecurityKey GetSigningKey()
        {
            var secret = _configuration["Auth:JwtSigningKey"];
            var environment = (_configuration["Auth:Environment"] ?? TaslowEnvironments.Development).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(secret))
            {
                if (environment.Equals(TaslowEnvironments.Production, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Auth:JwtSigningKey is required in production.");
                }

                secret = "taslow-development-auth-signing-key-change-before-production";
            }

            return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        }

        private static string? FindClaimValue(ClaimsPrincipal principal, params string[] claimTypes)
            => FindClaimValues(principal, claimTypes).FirstOrDefault();

        private static IEnumerable<string> FindClaimValues(ClaimsPrincipal principal, params string[] claimTypes)
        {
            var matches = principal.Claims
                .Where(claim => claimTypes.Any(type => ClaimTypeMatches(claim.Type, type)))
                .Select(claim => claim.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value));

            foreach (var value in matches)
            {
                yield return value;
            }
        }

        private static bool ClaimTypeMatches(string actualType, string requestedType)
        {
            var actual = actualType.Trim();
            var requested = requestedType.Trim();
            return actual.Equals(requested, StringComparison.OrdinalIgnoreCase)
                || actual.EndsWith($"/{requested}", StringComparison.OrdinalIgnoreCase)
                || actual.EndsWith($":{requested}", StringComparison.OrdinalIgnoreCase);
        }
    }
}
