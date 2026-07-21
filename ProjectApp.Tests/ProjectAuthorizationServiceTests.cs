using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Taslow.Project.Service;
using Taslow.Shared.Model;
using Xunit;

namespace ProjectApp.Tests;

public class ProjectAuthorizationServiceTests
{
    private const string SigningKey = "taslow-project-test-signing-key-with-at-least-32-bytes";
    private readonly ProjectAuthorizationService _authorization = new(new ConfigurationManager
    {
        ["Auth:Environment"] = TaslowEnvironments.Test,
        ["Auth:Issuer"] = "taslow-test-auth",
        ["Auth:Audience"] = "taslow-api",
        ["Auth:JwtSigningKey"] = SigningKey,
        ["Auth:CookieName"] = "taslow_auth"
    });

    [Fact]
    public void TenantAdminSession_ShouldCreateOnlyInsideItsTenant()
    {
        var auth = _authorization.Resolve(CookieHeaders(IssueToken(
            "tenant-a",
            "admin@bloomsky.onmicrosoft.com",
            TenantRoles.TenantAdmin)));

        _authorization.EnsureCanCreate(auth, "tenant-a");
        Assert.Throws<ProjectAuthorizationException>(() =>
            _authorization.EnsureCanCreate(auth, "tenant-b"));
    }

    [Fact]
    public void TenantPm_ShouldNotBootstrapProjectCreation()
    {
        var auth = _authorization.Resolve(CookieHeaders(IssueToken(
            "tenant-a",
            "manager@bloomsky.onmicrosoft.com",
            TenantRoles.TenantPm)));

        var error = Assert.Throws<ProjectAuthorizationException>(() =>
            _authorization.EnsureCanCreate(auth, "tenant-a"));
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, error.StatusCode);
    }

    [Fact]
    public void AssignedManagerSession_ShouldUseVerifiedEmailForProjectEdits()
    {
        var auth = _authorization.Resolve(CookieHeaders(IssueToken(
            "tenant-a",
            "manager@bloomsky.onmicrosoft.com",
            TenantRoles.TenantPm)));

        _authorization.EnsureCanManage(auth, "tenant-a");
        Assert.Equal("manager@bloomsky.onmicrosoft.com", auth.Email);
    }

    [Fact]
    public void BrowserSuppliedRoleHeadersWithoutSession_ShouldBeRejected()
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x-taslow-tenant-id"] = "tenant-a",
            ["x-taslow-roles"] = TenantRoles.TenantAdmin
        };

        var error = Assert.Throws<ProjectAuthorizationException>(() => _authorization.Resolve(headers));
        Assert.Equal(AuthErrorCodes.Required, error.Code);
    }

    private static Dictionary<string, string> CookieHeaders(string token)
        => new(StringComparer.OrdinalIgnoreCase) { ["Cookie"] = $"taslow_auth={token}" };

    private static string IssueToken(string tenantId, string email, params string[] roles)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, "subject-a"),
            new(TaslowClaimTypes.TenantId, tenantId),
            new(ClaimTypes.Email, email)
        };
        claims.AddRange(roles.Select(role => new Claim(TaslowClaimTypes.Roles, role)));

        var token = new JwtSecurityToken(
            issuer: "taslow-test-auth",
            audience: "taslow-api",
            claims: claims,
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(30),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
