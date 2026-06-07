---
description: Upgrade all 7 projects from net8.0 to net10.0 + update ASP.NET testing package
doc_id: plan.dotnet-upgrade
type: plan
status: complete
rigor_tier: L3
ttl_days: 30
stability: stable
ai_scope: editable
source_of_truth: false
---

# .NET 8 → 10 Upgrade

## TL;DR

> **Quick Summary**: Upgrade all 7 projektów z `net8.0` → `net10.0` + zaktualizować `Microsoft.AspNetCore.Mvc.Testing` 8.0.27 → 10.0. Dockerfile i CI już są na 10.0.
>
> **Deliverables**: 
> - 7 `.csproj`: TargetFramework `net8.0` → `net10.0`
> - 1 NuGet: `Microsoft.AspNetCore.Mvc.Testing` 8.0.27 → 10.0.x
> - Build + test verification
>
> **Estimated Effort**: 15 minut

---

## Work Objectives

### Core Objective
Ujednolicić wersję .NET — cały stack (kod, Docker, CI) na `net10.0`.

### Zakres zmian

| Plik | Zmiana |
|------|--------|
| `src/HybridTherapist.Api/*.csproj` | `net8.0` → `net10.0` |
| `src/HybridTherapist.Application/*.csproj` | `net8.0` → `net10.0` |
| `src/HybridTherapist.Domain/*.csproj` | `net8.0` → `net10.0` |
| `src/HybridTherapist.Infrastructure/*.csproj` | `net8.0` → `net10.0` |
| `src/HybridTherapist.Security/*.csproj` | `net8.0` → `net10.0` |
| `tests/HybridTherapist.Tests/*.csproj` | `net8.0` → `net10.0` + `Microsoft.AspNetCore.Mvc.Testing` 8.0.27 → 10.0.x |
| `tests/HybridTherapist.Integration/*.csproj` | `net8.0` → `net10.0` |

### Już zrobione
- Dockerfile: `sdk:10.0`, `aspnet:10.0` (PR #13)
- CI: `DOTNET_VERSION: "10.0.x"` (ci.yml)

---

## TODOs

- [x] 1. Upgrade 7 .csproj TargetFramework `net8.0` → `net10.0`

  **What to do**:
  - Edytuj 7 plików `.csproj` — zamień `<TargetFramework>net8.0</TargetFramework>` na `<TargetFramework>net10.0</TargetFramework>`
  - Pliki: `src/HybridTherapist.Api/HybridTherapist.Api.csproj`, `src/HybridTherapist.Application/HybridTherapist.Application.csproj`, `src/HybridTherapist.Domain/HybridTherapist.Domain.csproj`, `src/HybridTherapist.Infrastructure/HybridTherapist.Infrastructure.csproj`, `src/HybridTherapist.Security/HybridTherapist.Security.csproj`, `tests/HybridTherapist.Tests/HybridTherapist.Tests.csproj`, `tests/HybridTherapist.Integration/HybridTherapist.Integration.csproj`

  **Acceptance Criteria**:
  - [ ] `grep -r "net8.0" src/ tests/` → zero matches
  - [ ] `grep -r "net10.0" src/ tests/` → 7 matches

- [x] 2. Update Microsoft.AspNetCore.Mvc.Testing 8.0.27 → 10.0.8

  **What to do**:
  - Plik: `tests/HybridTherapist.Integration/HybridTherapist.Integration.csproj`
  - Sprawdź najnowszą wersję `Microsoft.AspNetCore.Mvc.Testing` na NuGet.org
  - Zamień `Version="8.0.27"` na najnowszy `10.0.x`

  **Acceptance Criteria**:
  - [ ] `grep "Microsoft.AspNetCore.Mvc.Testing" tests/HybridTherapist.Integration/HybridTherapist.Integration.csproj` pokazuje wersję 10.x

- [x] 3. Build + test verification

  **What to do**:
  - `dotnet build HybridTherapist.sln -c Release --nologo -v q` → 0 warnings, 0 errors
  - `dotnet test tests/HybridTherapist.Tests -c Release --no-build --nologo` → wszystkie przechodzą
  - `./scripts/run-hand-benchmark.sh --cassette --all-variants --report` → wszystkie przechodzą

  **Acceptance Criteria**:
  - [ ] Build: 0 warnings, 0 errors
  - [ ] Unit tests: wszystkie zielone
  - [ ] Benchmarki: wszystkie zielone

---

## Commit Strategy

| # | Commit Message | Files |
|---|---------------|-------|
| 1 | `build: upgrade .NET 8 → 10 across all projects` | 7 .csproj + 1 NuGet update |

---

## Success Criteria

```bash
# Brak net8.0 referencji
grep -r "net8.0" src/ tests/
# Expected: zero output

# net10.0 w 7 plikach
grep -rl "net10.0" src/ tests/ | wc -l
# Expected: 7

# Build
dotnet build HybridTherapist.sln -c Release --nologo -v q
# Expected: 0 warnings, 0 errors

# Testy
dotnet test tests/HybridTherapist.Tests -c Release --no-build --nologo
# Expected: all passed
```
