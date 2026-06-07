---
description: Fix Dockerfile HandCodec wget URLs from v0.2.0 to v0.3.0 for CI restore
doc_id: plan.dockerfile-codec-fix
type: plan
status: complete
rigor_tier: L3
ttl_days: 30
stability: stable
ai_scope: editable
source_of_truth: false
---

# Dockerfile HandCodec v0.3.0 URL Fix

## TL;DR

> **Quick Summary**: CI/CD pada na `dotnet restore` — Dockerfile ściąga HandCodec v0.2.0, ale kod wymaga v0.3.0. Fix: zmiana dwóch URL-i wgeta.
>
> **Deliverables**: 
> - `Dockerfile`: `v0.2.0` → `v0.3.0` w dwóch linijkach wgeta
>
> **Estimated Effort**: 2 minuty (1 commit)
> **Parallel Execution**: N/A — 1 zmiana, 1 plik

---

## Context

### Problem
PR #13 (dependabot/docker) zmergowany, zbumpował obrazy .NET z 8.0 do 10.0. CI/CD `dotnet restore` pada z:

```
error NU1102: Unable to find package HandCodec with version (>= 0.3.0)
  - Found 1 version(s) in local [ Nearest version: 0.2.0 ]
```

### Root Cause
Dockerfile (linie 7-8) wgetuje HandCodec `v0.2.0` z GitHub releases. Projekty `.csproj` wymagają `v0.3.0`. Release v0.3.0 istnieje na GitHubie.

---

## Work Objectives

### Core Objective
Przywrócić działanie CI/CD — zmienić URL-e wgeta z v0.2.0 na v0.3.0.

### Definition of Done
- [ ] `grep "v0.2.0" Dockerfile` → zero matches
- [ ] `grep "v0.3.0" Dockerfile` → 2 matches (oba URL-e wgeta)
- [ ] PR zmergowany do main
- [ ] CI/CD przechodzi (build + test)

---

## TODOs

- [x] 1. Fix Dockerfile wget URLs — v0.2.0 → v0.3.0

  **What to do**:
  - Dockerfile linie 7-8: zamień `v0.2.0` na `v0.3.0` w obu URL-ach wgeta
  - Linia 7: `releases/download/v0.2.0/HandCodec.0.2.0.nupkg` → `releases/download/v0.3.0/HandCodec.0.3.0.nupkg`
  - Linia 7: `local-packages/HandCodec.0.2.0.nupkg` → `local-packages/HandCodec.0.3.0.nupkg`
  - Linia 8: `releases/download/v0.2.0/HandRuntime.0.2.0.nupkg` → `releases/download/v0.3.0/HandRuntime.0.3.0.nupkg`
  - Linia 8: `local-packages/HandRuntime.0.2.0.nupkg` → `local-packages/HandRuntime.0.3.0.nupkg`

  **Must NOT do**:
  - Nie dotykaj nic poza liniami 7-8
  - Nie zmieniaj wersji .NET (już 10.0 — poprawne)

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: 2 string replacements w 1 pliku

  **Acceptance Criteria**:
  - [ ] `grep "v0.2.0" Dockerfile` → zero matches
  - [ ] `grep "v0.3.0" Dockerfile` → 2 matches

  **QA Scenarios**:
  ```
  Scenario: URLs updated correctly
    Tool: Bash (grep)
    Steps:
      1. grep "v0.2.0" Dockerfile
      2. grep "v0.3.0" Dockerfile
    Expected Result: Step 1 = empty, Step 2 = 2 matches
    Evidence: .omo/evidence/dockerfile-fix-grep.txt
  ```

  **Commit**: YES
  - Message: `fix: update Dockerfile wget HandCodec v0.2.0 → v0.3.0`

---

## Commit Strategy

| # | Commit Message | Files |
|---|---------------|-------|
| 1 | `fix: update Dockerfile wget HandCodec v0.2.0 → v0.3.0` | `Dockerfile` |

---

## Success Criteria

```bash
# Brak v0.2.0 w Dockerfile
grep "v0.2.0" Dockerfile
# Expected: zero output

# v0.3.0 obecne w obu URL-ach
grep "v0.3.0" Dockerfile
# Expected: 2 matches
```
