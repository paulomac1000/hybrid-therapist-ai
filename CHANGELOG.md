# Changelog

## 2026-05-23 — standard v2.0.0 compliance

- Upgraded to CI/CD Architect standard v2.0.0
- **BREAKING:** Migrated from `semgrep/semgrep-action@v1` to `semgrep/semgrep@v1` (upstream archived)
- **BREAKING:** .NET SDK upgraded from 8.0.x to 10.0.x
- Added `publishToken` support in Semgrep workflows
- Added `docker` ecosystem to Dependabot for automatic Dockerfile updates
- Merged Dependabot PRs: bumped dorny/test-reporter and actions/attest commit SHAs, 8 NuGet packages
