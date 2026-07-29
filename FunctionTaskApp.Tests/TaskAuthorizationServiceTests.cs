#nullable enable
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Taslow.Shared.Model;
using Taslow.Task.Service;
using Xunit;

namespace FunctionTaskApp.Tests;

public class TaskAuthorizationServiceTests
{
    private const string SigningKey = "taslow-task-test-signing-key-with-at-least-32-bytes";
    private readonly TaskAuthorizationService _authorization = new(new ConfigurationManager
    {
        ["Auth:Environment"] = TaslowEnvironments.Test,
        ["Auth:Issuer"] = "taslow-test-auth",
        ["Auth:Audience"] = "taslow-api",
        ["Auth:JwtSigningKey"] = SigningKey,
        ["Auth:CookieName"] = "taslow_auth"
    });

    [Fact]
    public void ValidSession_ShouldResolveOnlySignedIdentityClaims()
    {
        var token = IssueToken(
            "tenant-a",
            "manager@bloomsky.onmicrosoft.com",
            new[] { TenantRoles.TenantPm },
            new[] { "civilian" });

        var auth = _authorization.Resolve(new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {token}",
            ["x-taslow-roles"] = TenantRoles.TenantAdmin,
            ["x-taslow-user-email"] = "forged@bloomsky.onmicrosoft.com"
        });

        Assert.Equal("tenant-a", auth.TenantId);
        Assert.Equal("manager@bloomsky.onmicrosoft.com", auth.Email);
        Assert.Equal(new[] { TenantRoles.TenantPm }, auth.Roles);
        Assert.Equal(new[] { "CIVILIAN" }, auth.LeaderMarketCodes);
        Assert.Equal(token, auth.AccessToken);
    }

    [Fact]
    public void BrowserIdentityHeadersWithoutSession_ShouldBeRejected()
    {
        var error = Assert.Throws<TaskAuthorizationException>(() =>
            _authorization.Resolve(new Dictionary<string, string>
            {
                ["x-taslow-tenant-id"] = "tenant-a",
                ["x-taslow-roles"] = TenantRoles.TenantAdmin
            }));

        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.Equal(AuthErrorCodes.Required, error.Code);
    }

    [Fact]
    public void ExpiredSignedSession_ShouldBeRejected()
    {
        var token = IssueToken(
            "tenant-a",
            "admin@bloomsky.onmicrosoft.com",
            new[] { TenantRoles.TenantAdmin },
            expires: DateTime.UtcNow.AddMinutes(-5),
            notBefore: DateTime.UtcNow.AddMinutes(-30));

        var error = Assert.Throws<TaskAuthorizationException>(() =>
            _authorization.Resolve(new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {token}"
            }));

        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.Equal(AuthErrorCodes.TokenExpired, error.Code);
    }

    [Fact]
    public void TenantAndSelfChecks_ShouldRejectCrossBoundaryReads()
    {
        var auth = _authorization.Resolve(CookieHeaders(IssueToken(
            "tenant-a",
            "alex@bloomsky.onmicrosoft.com",
            new[] { TenantRoles.TenantUser })));

        _authorization.EnsureTenant(auth, "tenant-a");
        _authorization.EnsureSelf(auth, "alex@bloomsky.onmicrosoft.com");
        Assert.Throws<TaskAuthorizationException>(() =>
            _authorization.EnsureTenant(auth, "tenant-b"));
        Assert.Throws<TaskAuthorizationException>(() =>
            _authorization.EnsureSelf(auth, "other@bloomsky.onmicrosoft.com"));
    }

    [Fact]
    public void ManagedProjectReads_ShouldRequireProjectManagerRole()
    {
        var pm = _authorization.Resolve(CookieHeaders(IssueToken(
            "tenant-a",
            "manager@bloomsky.onmicrosoft.com",
            new[] { TenantRoles.TenantPm })));
        var user = _authorization.Resolve(CookieHeaders(IssueToken(
            "tenant-a",
            "user@bloomsky.onmicrosoft.com",
            new[] { TenantRoles.TenantUser })));

        _authorization.EnsureProjectManager(pm);
        Assert.Throws<TaskAuthorizationException>(() =>
            _authorization.EnsureProjectManager(user));
    }

    private static Dictionary<string, string> CookieHeaders(string token)
        => new(StringComparer.OrdinalIgnoreCase) { ["Cookie"] = $"taslow_auth={token}" };

    private static string IssueToken(
        string tenantId,
        string email,
        IEnumerable<string> roles,
        IEnumerable<string>? leaderMarketCodes = null,
        DateTime? expires = null,
        DateTime? notBefore = null)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, "subject-a"),
            new(TaslowClaimTypes.TenantId, tenantId),
            new(ClaimTypes.Email, email)
        };
        claims.AddRange(roles.Select(role => new Claim(TaslowClaimTypes.Roles, role)));
        claims.AddRange((leaderMarketCodes ?? Array.Empty<string>())
            .Select(code => new Claim(TaslowClaimTypes.LeaderMarketCodes, code)));

        var token = new JwtSecurityToken(
            issuer: "taslow-test-auth",
            audience: "taslow-api",
            claims: claims,
            notBefore: notBefore ?? now.AddMinutes(-1),
            expires: expires ?? now.AddMinutes(30),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
