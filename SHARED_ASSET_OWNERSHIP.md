# Shared Asset Ownership

The single accountable `FunctionTaskApp Shared Platform Owner` is GitHub owner `@jessegoraya`.

This owner must approve changes to:

- `FunctionTaskApp.sln`
- `Directory.Packages.props`
- `Taslow.Shared/**`
- `.github/workflows/**`
- `.github/CODEOWNERS`

Service maintainers may propose changes, but shared changes must build and test the full solution before merge. Repository branch protection must require the Code Owner review defined in `.github/CODEOWNERS`.
