# Function Services Release Notes

## v0.1.0-dev.14

- Adds a signed, tenant-scoped minimal active-Project catalog for manual Task reassignment without widening the normal Project directory.
- Validates that a moved Task targets an active Project and that its newly selected assignee is a manager or associated person on that Project.
- Persists the Project move, replacement assignee, and any same-dialog Task field edits together while denying mutation by unrelated users.

## v0.1.0-dev.13

- Posts Microsoft authorization callbacks as form data so large Entra authorization codes do
  not exceed the Azure Functions query-string limit.
- Retains GET handling for administrator-consent callbacks while accepting POST for interactive
  user authentication.
- Adds regression coverage for 4 KB authorization codes and form-posted provider errors.

## v0.1.0-dev.12

- Makes the shared Cosmos DB connection mode environment-configurable while preserving the
  existing default when no mode is supplied.
- Enables the VNet-integrated Test Functions to use Gateway mode and reduce outbound TCP/SNAT
  pressure observed during the governed peak-load profile.
- Rejects unsupported connection-mode values and covers connection-string and managed-identity
  client creation with unit tests.

## v0.1.0-dev.11

- Enriches Project agent-context users with canonical display names from the tenant directory
  before the Foundry email agent resolves task assignees.
- Preserves legacy mailbox handles as aliases so existing deterministic references continue to
  match while production-shaped names such as `Bradford Ebright` resolve correctly.
- Keeps the lookup tenant-partitioned and covered by the Project Function's existing
  database-scoped managed-identity access.

## v0.1.0-dev.10

- Preserves caller-assigned Group Task identifiers through the Task write path so governed email tasks retain their deterministic idempotency identity.
- Continues generating identifiers for interactive and legacy callers that omit the Group Task identifier or submit the empty GUID.
- Adds regression coverage for both deterministic email-agent writes and server-generated identifiers.

## v0.1.0-dev.9

- Normalizes Microsoft Graph `sentDateTime` values to invariant UTC ISO 8601 before invoking the Foundry email-extraction agent.
- Prevents locale-formatted timestamps from failing the hosted-agent request contract with HTTP 400.
- Adds Graph-hydration and raw Foundry-request regression coverage for the exact serialized timestamp.

## v0.1.0-dev.8

- Adds read-minimal ingestion and task evidence operations for the governed BloomSky email-to-task campaign.
- Requires a distinct APIM-established Test runner marker so deployment and email-runtime identities cannot use the campaign control plane.
- Adds deterministic, source-checked removal of only the Group Tasks created by a campaign while retaining ingestion-state audit records.

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
