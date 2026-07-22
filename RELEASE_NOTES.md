# Function Services Release Notes

## v0.1.0-dev.7

- Restricts Project agent-context hydration and Task idempotency reads to an APIM-established internal email-ingestion workload marker.
- Adds a read-minimal Task existence endpoint so the Tenant email runtime no longer retrieves an entire Group Task Set during duplicate detection.
- Keeps the managed-identity bearer token on both internal APIM calls and adds regression coverage for exact marker, route, token, and duplicate behavior.

## v0.1.0-dev.6

- Adds governed Tenant user catalog read/reconciliation endpoints for production-shaped directory onboarding.
- Persists explicit `tenant_admin`, `tenant_leader`, and `tenant_user` assignments while preventing tenant onboarding from granting `taslow_admin` or Project-owned `tenant_pm`.
- Resolves integrated Microsoft sessions from the persisted tenant roles so an approved Tenant Administrator can create the tenant's first Project through the Taslow UX.
- Preserves existing leader Market Code scopes during directory reconciliation and continues enforcing tenant boundaries and optimistic concurrency.

## v0.1.0-dev.5

- Allows a signed `tenant_admin` session to create a complete Project within its own tenant while requiring at least one explicit Project Manager and scope.
- Validates Project authorization from the shared signed bearer token or HttpOnly cookie and rejects forged browser identity headers and cross-tenant creation.
- Persists Market Code, members, managers, and scopes in the initial Project document; the creating Tenant Admin is not promoted to Project Manager automatically.
- Retains assigned `tenant_pm` authorization for existing Project edits and adds regression coverage for role, tenant, token, and request-validation boundaries.

## v0.1.0-dev.4

- Treats Microsoft administrator-consent callbacks as successful consent acknowledgments instead of attempting an authorization-code exchange.
- Keeps administrator consent separate from interactive user login: administrators return to Taslow and start Microsoft sign-in after consent.
- Adds regression coverage for administrator consent, authorization-code login, provider errors, and malformed Microsoft callbacks.
- Persists hosted cross-site authentication sessions with `SameSite=None; Secure` cookies while retaining `SameSite=Lax` for local HTTP development.
- Adds regression coverage for secure session creation, local development, and logout cookie expiration.

## v0.1.0-dev.3

- Documents that Microsoft authorization-code exchange requires the Entra client-secret value stored through a Key Vault reference, never the credential ID.
- Documents that the pre-login integrated-tenant directory selector uses application `User.Read.All` with customer-tenant administrator consent; delegated `User.Read` remains the interactive login scope.
- Keeps the configured integrated-user list as a Dev fallback only. Release acceptance must prove multi-user Graph enumeration so fallback-only behavior cannot be mistaken for complete tenant onboarding.

## v0.1.0-dev.2

- Preserves the configured Project API base path when Task Analytics requests active, managed, or batched projects through APIM.
- Adds client route-composition tests to prevent silent empty Analytics portfolios caused by malformed upstream URLs.

## v0.1.0-dev.1

- Migrates Task, Project, and Tenant Azure Functions to the .NET 10 isolated worker architecture.
- Adds Task, Project, Tenant, authentication, analytics, email-ingestion, and scope-linking tests.
- Adds tenant-aware contracts, role and authorization services, Project agent context, and Task analytics endpoints.
- Enables warning-free builds, test-result and coverage publication, dependency auditing, and immutable Function ZIP artifacts.
- Deployment requires coordinated Function runtime settings, APIM routes, Logic App settings, and the Taslow platform release manifest.
