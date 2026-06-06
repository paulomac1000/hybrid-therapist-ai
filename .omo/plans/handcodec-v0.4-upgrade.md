# HandCodec/HandRuntime v0.3.0 → v0.4.0 Upgrade

## TL;DR

> **Quick Summary**: Aktualizacja HandCodec i HandRuntime z v0.3.0 do v0.4.0 (obsługa .NET 10). 4 pliki .csproj + Dockerfile + local-packages.
>
> **Deliverables**: 
> - 4 .csproj: `HandCodec 0.3.0` / `HandRuntime 0.3.0` → `0.4.0`
> - Dockerfile: wget URLs v0.3.0 → v0.4.0
> - local-packages/: stare .nupkg usunięte, nowe pobrane
>
> **Estimated Effort**: ~5 min (4 string replacements + download)
> **Project is already on .NET 10** — ten upgrade dotyczy tylko wersji bibliotek

---

## Work Objectives

### Core Objective
Podnieść HandCodec i HandRuntime do v0.4.0, która natywnie wspiera .NET 10.

### Pliki do zmiany

| Plik | Obecnie | Docelowo |
|------|---------|----------|
| `src/HybridTherapist.Application/HybridTherapist.Application.csproj` | `HandCodec 0.3.0`, `HandRuntime 0.3.0` | `0.4.0` |
| `src/HybridTherapist.Infrastructure/HybridTherapist.Infrastructure.csproj` | `HandRuntime 0.3.0` | `0.4.0` |
| `tests/HybridTherapist.Tests/HybridTherapist.Tests.csproj` | `HandRuntime 0.3.0` | `0.4.0` |
| `Dockerfile` | wget `v0.3.0` | wget `v0.4.0` |

---

## TODOs

- [x] 1. Download new packages to local-packages/

  **What to do**:
  - Pobierz `HandCodec.0.4.0.nupkg` i `HandRuntime.0.4.0.nupkg` z GitHub releases
  - Usuń stare `HandCodec.0.3.0.nupkg` i `HandRuntime.0.3.0.nupkg`
  - URL: `https://github.com/paulomac1000/hand-codec/releases/download/v0.4.0/`

  **Agent Profile**: `quick`

- [x] 2. Update 4 .csproj PackageReference: 0.3.0 → 0.4.0

  **What to do**:
  - `src/HybridTherapist.Application/HybridTherapist.Application.csproj` — HandCodec + HandRuntime
  - `src/HybridTherapist.Infrastructure/HybridTherapist.Infrastructure.csproj` — HandRuntime
  - `tests/HybridTherapist.Tests/HybridTherapist.Tests.csproj` — HandRuntime

  **Agent Profile**: `quick`

- [x] 3. Update Dockerfile wget URLs: v0.3.0 → v0.4.0

  **Agent Profile**: `quick`

- [x] 4. Build + test verification

  **Agent Profile**: `quick`

---

## Commit Strategy

| Commit | Files |
|--------|-------|
| `deps: upgrade HandCodec and HandRuntime v0.3.0 → v0.4.0` | 4 .csproj + Dockerfile + local-packages |

---

## Success Criteria

```bash
# Wszystkie .csproj używają 0.4.0
grep -r "0.3.0" $(find . -name "*.csproj" -not -path "*/bin/*" -not -path "*/obj/*") | grep Hand
# Expected: zero output

# Dockerfile wget v0.4.0
grep "v0.4.0" Dockerfile
# Expected: 2 matches

# Build
dotnet build HybridTherapist.sln -c Release --nologo -v q
# Expected: 0 warnings, 0 errors
```
