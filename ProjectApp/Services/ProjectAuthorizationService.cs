using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Taslow.Project.Model;
using Taslow.Project.Service.Interface;
using Taslow.Shared.Model;

namespace Taslow.Project.Service;

public sealed class ProjectAuthorizationService : IProjectAuthorizationService
{
    private readonly IConfiguration _configuration;
    private readonly JwtSecurityTokenHandler _handler = new() { MapInboundClaims = false };

    public ProjectAuthorizationService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public ProjectAuthContext Resolve(IDictionary<string, string> headers)
    {
        var token = ExtractBearerToken(headers) ?? ExtractCookieToken(headers);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw Unauthorized(AuthErrorCodes.Required, "A valid Taslow session is required.");
        }

        try
        {
            var principal = _handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _configuration["Auth:Issuer"] ?? "taslow-dev-auth",
                ValidateAudience = true,
                ValidAudience = _configuration["Auth:Audience"] ?? "taslow-api",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = GetSigningKey(),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            }, out var securityToken);

            if (securityToken is not JwtSecurityToken jwt
                || !jwt.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw Unauthorized(AuthErrorCodes.TokenInvalid, "Taslow token signature algorithm is invalid.");
            }

            var roles = FindClaimValues(principal, TaslowClaimTypes.Roles, ClaimTypes.Role, "role")
                .Select(value => value.ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (roles.Count == 0 || roles.Any(role => !TenantRoles.All.Contains(role)))
            {
                throw Unauthorized(AuthErrorCodes.TokenInvalid, "Taslow token contains invalid role claims.");
            }

            var tenantId = FindClaimValue(principal, TaslowClaimTypes.TenantId) ?? string.Empty;
            var subject = FindClaimValue(principal, JwtRegisteredClaimNames.Sub, ClaimTypes.NameIdentifier) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(subject))
            {
                throw Unauthorized(AuthErrorCodes.TokenInvalid, "Taslow token is missing tenant or subject claims.");
            }

            return new ProjectAuthContext
            {
                TenantId = tenantId,
                Subject = subject,
                Email = FindClaimValue(principal, ClaimTypes.Email, JwtRegisteredClaimNames.Email, "email") ?? string.Empty,
                AccessToken = token,
                Roles = roles,
                LeaderMarketCodes = FindClaimValues(principal, TaslowClaimTypes.LeaderMarketCodes)
                    .Select(value => value.ToUpperInvariant())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }
        catch (SecurityTokenExpiredException)
        {
            throw Unauthorized(AuthErrorCodes.TokenExpired, "Taslow session is expired.");
        }
        catch (ProjectAuthorizationException)
        {
            throw;
        }
        catch (Exception)
        {
            throw Unauthorized(AuthErrorCodes.TokenInvalid, "Taslow session is invalid.");
        }
    }

    public void EnsureCanCreate(ProjectAuthContext auth, string tenantId)
    {
        EnsureTenant(auth, tenantId);
        if (!auth.Roles.Contains(TenantRoles.TenantAdmin, StringComparer.OrdinalIgnoreCase))
        {
            throw Forbidden("Only a Tenant Admin can create a Project.");
        }
    }

    public void EnsureCanManage(ProjectAuthContext auth, string tenantId)
    {
        EnsureTenant(auth, tenantId);
        if (!auth.Roles.Contains(TenantRoles.TenantPm, StringComparer.OrdinalIgnoreCase))
        {
            throw Forbidden("Only an assigned Project Manager can edit this Project.");
        }

        if (string.IsNullOrWhiteSpace(auth.Email))
        {
            throw Unauthorized(AuthErrorCodes.TokenInvalid, "Taslow session is missing the user email claim.");
        }
    }

    public void EnsureCanReadProjectDetails(ProjectAuthContext auth, string tenantId)
    {
        EnsureTenant(auth, tenantId);
        if (!auth.Roles.Any(role =>
                role.Equals(TenantRoles.TenantAdmin, StringComparison.OrdinalIgnoreCase)
                || role.Equals(TenantRoles.TaslowAdmin, StringComparison.OrdinalIgnoreCase)
                || role.Equals(TenantRoles.TenantPm, StringComparison.OrdinalIgnoreCase)))
        {
            throw Forbidden("Only a Tenant Admin, Taslow Admin, or Project Manager can read Project details.");
        }
    }

    public void EnsureCanReadManagedProjects(
        ProjectAuthContext auth,
        string tenantId,
        string managerEmail)
    {
        EnsureTenant(auth, tenantId);
        if (auth.Roles.Contains(TenantRoles.TenantAdmin, StringComparer.OrdinalIgnoreCase)
            || auth.Roles.Contains(TenantRoles.TaslowAdmin, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        if (!auth.Roles.Contains(TenantRoles.TenantPm, StringComparer.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(auth.Email)
            || !auth.Email.Equals(managerEmail?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw Forbidden("Caller is not authorized for another Project Manager's Projects.");
        }
    }

    public void EnsureTenant(ProjectAuthContext auth, string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId)
            || !auth.TenantId.Equals(tenantId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw Forbidden("Caller is not authorized for this tenant.");
        }
    }

    private SymmetricSecurityKey GetSigningKey()
    {
        var secret = _configuration["Auth:JwtSigningKey"];
        var environment = _configuration["Auth:Environment"] ?? TaslowEnvironments.Development;
        if (string.IsNullOrWhiteSpace(secret))
        {
            if (!environment.Equals(TaslowEnvironments.Development, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Auth:JwtSigningKey is required outside Development.");
            }

            secret = "taslow-development-auth-signing-key-change-before-production";
        }

        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
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

    private static string? FindClaimValue(ClaimsPrincipal principal, params string[] claimTypes)
        => FindClaimValues(principal, claimTypes).FirstOrDefault();

    private static IEnumerable<string> FindClaimValues(ClaimsPrincipal principal, params string[] claimTypes)
        => principal.Claims
            .Where(claim => claimTypes.Any(type => claim.Type.Equals(type, StringComparison.OrdinalIgnoreCase)))
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value));

    private static ProjectAuthorizationException Unauthorized(string code, string message)
        => new(HttpStatusCode.Unauthorized, code, message);

    private static ProjectAuthorizationException Forbidden(string message)
        => new(HttpStatusCode.Forbidden, AuthErrorCodes.RoleForbidden, message);
}

public sealed class ProjectAuthorizationException : Exception
{
    public ProjectAuthorizationException(HttpStatusCode statusCode, string code, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public HttpStatusCode StatusCode { get; }

    public string Code { get; }
}
