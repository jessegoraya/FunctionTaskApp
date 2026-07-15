# Function Services Release Notes

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
