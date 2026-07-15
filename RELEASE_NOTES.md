# Function Services Release Notes

## v0.1.0-dev.2

- Preserves the configured Project API base path when Task Analytics requests active, managed, or batched projects through APIM.
- Adds client route-composition tests to prevent silent empty Analytics portfolios caused by malformed upstream URLs.

## v0.1.0-dev.1

- Migrates Task, Project, and Tenant Azure Functions to the .NET 10 isolated worker architecture.
- Adds Task, Project, Tenant, authentication, analytics, email-ingestion, and scope-linking tests.
- Adds tenant-aware contracts, role and authorization services, Project agent context, and Task analytics endpoints.
- Enables warning-free builds, test-result and coverage publication, dependency auditing, and immutable Function ZIP artifacts.
- Deployment requires coordinated Function runtime settings, APIM routes, Logic App settings, and the Taslow platform release manifest.
