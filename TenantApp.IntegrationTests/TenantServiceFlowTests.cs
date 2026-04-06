using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
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
            ITenantAuthorizationService authorization = new TenantAuthorizationService();
            ITenantService service = new TenantService(repository, validation, authorization);

            var auth = new TenantAuthContext { Role = TenantRoles.TaslowAdmin };

            var created = await service.CreateAsync(BuildCreateRequest("Acme Construction"), auth);

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
            ITenantAuthorizationService authorization = new TenantAuthorizationService();
            ITenantService service = new TenantService(repository, validation, authorization);

            var auth = new TenantAuthContext { Role = TenantRoles.TaslowAdmin };
            var created = await service.CreateAsync(BuildCreateRequest("Contoso"), auth);

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

        private static TenantCreateRequest BuildCreateRequest(string displayName)
        {
            return new TenantCreateRequest
            {
                DisplayName = displayName,
                Provider = TenantProviders.Microsoft,
                CompanyPocName = "Pat Manager",
                CompanyPocTitle = "Operations Manager",
                CompanyPocEmail = "pat.manager@example.com",
                CompanyPocPhone = "+1 555 123 4567",
                MailingAddressLine1 = "123 Main St",
                MailingAddressLine2 = "Suite 200",
                MailingCity = "Boston",
                MailingStateProvince = "MA",
                MailingPostalCode = "02108",
                MailingCountryCode = "US"
            };
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
