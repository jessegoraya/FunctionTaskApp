# Function Services Release Notes

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
