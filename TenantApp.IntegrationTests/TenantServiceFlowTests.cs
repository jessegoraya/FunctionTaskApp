using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Taslow.Shared.Model;
using Taslow.Tenant.DAL.Interface;
using Taslow.Tenant.Model;
using Taslow.Tenant.Service;
using Taslow.Tenant.Service.Interface;
using Xunit;

namespace TenantApp.IntegrationTests
{
    public class TenantServiceFlowTests
    {
        [Fact]
        public async Task CreateAndPatchTenantFlow_ShouldRespectAuthAndEtag()
        {
            ITenantRepository repository = new InMemoryTenantRepository();
            ITenantValidationService validation = new TenantValidationService();
            ITenantAuthorizationService authorization = CreateAuthorizationService();
            ITenantService service = new TenantService(repository, validation, authorization);

            var auth = new TenantAuthContext { Role = TenantRoles.TaslowAdmin };

            var created = await service.CreateAsync(CreateTenantRequest("Acme Construction"), auth);

            Assert.False(string.IsNullOrWhiteSpace(created.TenantId));
            Assert.False(string.IsNullOrWhiteSpace(created.ETag));

            var patched = await service.PatchTenantAsync(
                created.TenantId,
                new TenantDetailsPatchRequest { Status = TenantStatuses.Active },
                created.ETag,
                auth);

            Assert.Equal(TenantStatuses.Active, patched.Data.Tenant.Status);
        }

        [Fact]
        public async Task PatchTenantFlow_ShouldReturnPreconditionFailed_ForStaleETag()
        {
            ITenantRepository repository = new InMemoryTenantRepository();
            ITenantValidationService validation = new TenantValidationService();
            ITenantAuthorizationService authorization = CreateAuthorizationService();
            ITenantService service = new TenantService(repository, validation, authorization);

            var auth = new TenantAuthContext { Role = TenantRoles.TaslowAdmin };
            var created = await service.CreateAsync(CreateTenantRequest("Contoso"), auth);

            await service.PatchTenantAsync(
                created.TenantId,
                new TenantDetailsPatchRequest { DisplayName = "Contoso Updated" },
                created.ETag,
                auth);

            var ex = await Assert.ThrowsAsync<TenantApiException>(async () =>
            {
                await service.PatchTenantAsync(
                    created.TenantId,
                    new TenantDetailsPatchRequest { DisplayName = "Wrong ETag Attempt" },
                    created.ETag,
                    auth);
            });

            Assert.Equal(HttpStatusCode.PreconditionFailed, ex.StatusCode);
        }

        [Fact]
        public async Task PatchTenantUsersFlow_ShouldPersistOnlyTenantAssignableRoles()
        {
            ITenantRepository repository = new InMemoryTenantRepository();
            ITenantValidationService validation = new TenantValidationService();
            ITenantAuthorizationService authorization = CreateAuthorizationService();
            ITenantService service = new TenantService(repository, validation, authorization);

            var auth = new TenantAuthContext { Role = TenantRoles.TaslowAdmin };
            var created = await service.CreateAsync(CreateTenantRequest("Role Test"), auth);
            var patched = await service.PatchUsersAsync(
                created.TenantId,
                new TenantUsersPatchRequest
                {
                    Users = new List<TenantUserRoleAssignmentRequest>
                    {
                        new()
                        {
                            UserId = "tenant-admin-1",
                            DisplayName = "Tenant Admin",
                            Email = "admin@example.com",
                            Roles = new List<string> { TenantRoles.TenantAdmin }
                        },
                        new()
                        {
                            UserId = "tenant-leader-1",
                            DisplayName = "Tenant Leader",
                            Email = "leader@example.com",
                            Roles = new List<string> { TenantRoles.TenantLeader }
                        }
                    }
                },
                created.ETag,
                auth);

            Assert.Equal(2, patched.Users.Count);
            Assert.Equal(TenantRoles.TenantAdmin, patched.Users.Single(user => user.UserId == "tenant-admin-1").Roles.Single());
            Assert.Equal(TenantRoles.TenantLeader, patched.Users.Single(user => user.UserId == "tenant-leader-1").Roles.Single());
            Assert.DoesNotContain(patched.Users.SelectMany(user => user.Roles), role => role == TenantRoles.TaslowAdmin || role == TenantRoles.TenantPm);

            var read = await service.GetUsersAsync(created.TenantId, auth);
            Assert.Equal(patched.ETag, read.ETag);
            Assert.Equal(2, read.Users.Count);
        }

        private static TenantCreateRequest CreateTenantRequest(string displayName)
        {
            return new TenantCreateRequest
            {
                DisplayName = displayName,
                Provider = TenantProviders.Microsoft,
                CompanyPocName = "Jordan Lee",
                CompanyPocTitle = "Program Manager",
                CompanyPocEmail = "jordan.lee@example.com",
                CompanyPocPhone = "+1 555-0100",
                MailingAddressLine1 = "100 Main Street",
                MailingCity = "Arlington",
                MailingStateProvince = "VA",
                MailingPostalCode = "22201",
                MailingCountryCode = "US"
            };
        }

        private static ITenantAuthorizationService CreateAuthorizationService()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:Environment"] = TaslowEnvironments.Test,
                    ["Auth:JwtSigningKey"] = "taslow-test-integration-signing-key-for-tests"
                })
                .Build();
            return new TenantAuthorizationService(
                new TaslowJwtService(configuration),
                configuration);
        }
    }

    internal class InMemoryTenantRepository : ITenantRepository
    {
        private readonly Dictionary<string, (TenantDocumentDTO Doc, string ETag)> _items = new();

        public Task<(TenantDocumentDTO Document, string ETag)> CreateAsync(TenantDocumentDTO document, CancellationToken cancellationToken = default)
        {
            if (_items.ContainsKey(document.Id))
            {
                throw new TenantApiException(HttpStatusCode.Conflict, TenantErrorCodes.DuplicateTenant, "Tenant already exists.");
            }

            var etag = NewEtag();
            _items[document.Id] = (Clone(document), etag);
            return Task.FromResult((Clone(document), etag));
        }

        public Task<(TenantDocumentDTO? Document, string? ETag)> GetByIdAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            if (_items.TryGetValue(tenantId, out var found))
            {
                return Task.FromResult<(TenantDocumentDTO?, string?)>((Clone(found.Doc), found.ETag));
            }

            return Task.FromResult<(TenantDocumentDTO?, string?)>((null, null));
        }

        public Task<(List<TenantDocumentDTO> Items, string? ContinuationToken)> ListAsync(TenantListQuery query, CancellationToken cancellationToken = default)
        {
            IEnumerable<TenantDocumentDTO> items = _items.Values.Select(x => Clone(x.Doc));

            var status = string.IsNullOrWhiteSpace(query.Status) ? TenantStatuses.Active : query.Status;
            if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
            {
                items = items.Where(i => i.Tenant.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                items = items.Where(i => i.Tenant.DisplayName.StartsWith(query.Search, StringComparison.OrdinalIgnoreCase));
            }

            return Task.FromResult((items.Take(query.PageSize).ToList(), (string?)null));
        }

        public Task<(TenantDocumentDTO Document, string ETag)> ReplaceAsync(TenantDocumentDTO document, string ifMatchETag, CancellationToken cancellationToken = default)
        {
            if (!_items.TryGetValue(document.Id, out var found))
            {
                throw new TenantApiException(HttpStatusCode.NotFound, TenantErrorCodes.NotFound, "Tenant not found.");
            }

            if (!string.Equals(found.ETag, ifMatchETag, StringComparison.Ordinal))
            {
                throw new TenantApiException(HttpStatusCode.PreconditionFailed, TenantErrorCodes.PreconditionFailed, "ETag mismatch.");
            }

            var etag = NewEtag();
            _items[document.Id] = (Clone(document), etag);
            return Task.FromResult((Clone(document), etag));
        }

        private static string NewEtag() => $"\"{Guid.NewGuid()}\"";

        private static TenantDocumentDTO Clone(TenantDocumentDTO source)
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(source);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<TenantDocumentDTO>(json) ?? new TenantDocumentDTO();
        }
    }
}
