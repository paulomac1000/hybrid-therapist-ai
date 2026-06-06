# SonarQube INFO Issues Cleanup — 28 Issues

## TL;DR

> **Quick Summary**: Wyczyścić ostatnie 28 issue z SonarQube (wszystkie INFO) — głównie `CA1861` (const array → static readonly) i `xUnit1042` (object[] → TheoryData<>).
>
> **Deliverables**:
> - 7 plików testowych: 20× `CA1861` naprawione
> - 5 plików testowych: 5× `xUnit1042` naprawione
> - 1 plik produkcyjny: `CA1822` naprawione
>
> **Estimated Effort**: ~15 minut (wszystkie zmiany w testach, niskie ryzyko regresji)
> **Parallel Execution**: YES — 3 waves, do 8 tasks równolegle

---

## Context

### SonarQube stan
- **Bugs: 0, Vulnerabilities: 0, Security Hotspots: 0**
- **Code Smells: 28** (wszystkie INFO)
- **Ratings: A (1.0)** — Reliability, Security, Maintainability
- Quality Gate ERROR tylko przez brak coverage uploadu (nie problem kodu)

### Klasyfikacja 28 issue

| Rule | Severity | Count | Pattern | Files affected |
|------|----------|-------|---------|----------------|
| `CA1861` | INFO | 20 | Constant array arguments → `static readonly` | HandJsonBenchmarkTests, HandPlaintextBenchmarkTests, HandSemanticBenchmarkTests, HandBenchmarkValidator, TopicRegistryTests |
| `xUnit1042` | INFO | 5 | `MemberData` returning `object[]` → `TheoryData<>` | HandBenchmarkTests, HandJsonBenchmarkTests, HandPlaintextBenchmarkTests, HandSemanticBenchmarkTests, HandCheckpointLibraryTests |
| `CA1822` | INFO | 1 | Method can be marked `static` | PrivacySanitizer.cs |
| `CA1861` (misc) | INFO | 2 | Constant array in TopicRegistryTests | TopicRegistryTests |

---

## Work Objectives

### Core Objective
Doprowadzić SonarQube do **0 issue** we wszystkich kategoriach.

### Definition of Done
- [~] `sonarqube_search_sonar_issues_in_projects` → 0 results
- [~] `sonarqube_get_component_measures` → code_smells = 0
- [~] Build: 0 warnings, 0 errors
- [~] Unit tests: 280/280 passed
- [~] Benchmark tests: wszystkie przechodzą (cassette mode)

---

## TODOs

### Wave 1 — xUnit1042 (5 plików, MAX PARALLEL)

**Wzorzec fixu**: Zamień `public static IEnumerable<object[]> MethodName()` na `public static TheoryData<type1, type2> MethodName()`. Każda zmiana dotyczy jednej metody w jednym pliku.

- [x] 1. Fix xUnit1042 in `HandBenchmarkTests.cs` (line 53)

  **What to do**: Zamień sygnaturę `AllScenarioCassettes()` z `IEnumerable<object[]>` na `TheoryData<string>`. Usuń `yield return new object[] { x }` → `yield return x`.

  **File**: `tests/HybridTherapist.Integration/HandBenchmarkTests.cs`
  **Agent Profile**: `quick`

- [x] 2. Fix xUnit1042 in `HandJsonBenchmarkTests.cs` (line 32)

  **File**: `tests/HybridTherapist.Integration/HandJsonBenchmarkTests.cs`
  **Agent Profile**: `quick`

- [x] 3. Fix xUnit1042 in `HandPlaintextBenchmarkTests.cs` (line 32)

  **File**: `tests/HybridTherapist.Integration/HandPlaintextBenchmarkTests.cs`
  **Agent Profile**: `quick`

- [x] 4. Fix xUnit1042 in `HandSemanticBenchmarkTests.cs` (line 32)

  **File**: `tests/HybridTherapist.Integration/HandSemanticBenchmarkTests.cs`
  **Agent Profile**: `quick`

- [x] 5. Fix xUnit1042 in `HandCheckpointLibraryTests.cs` (line 19)

  **File**: `tests/HybridTherapist.Tests/HandCheckpointLibraryTests.cs`
  **Agent Profile**: `quick`

**Acceptance Criteria (per task)**:
- [ ] `xUnit1042` nie pojawia się w SonarQube dla tego pliku
- [ ] Build przechodzi, testy przechodzą

### Wave 2 — CA1861 (20 issues w plikach testowych, MAX PARALLEL)

**Wzorzec fixu**: Wyciągnij stałe tablice z wywołań metod do `static readonly` pól klasy.

- [x] 6. Fix CA1861 w `HandBenchmarkValidator.cs` (line 140)

- [x] 7. Fix CA1861 w `HandJsonBenchmarkTests.cs` (lines 93, 101, 102, 120, 128, 129) — 6 issues

- [x] 8. Fix CA1861 w `HandPlaintextBenchmarkTests.cs` (lines 93, 101, 102, 120, 128, 129) — 6 issues

- [x] 9. Fix CA1861 w `HandSemanticBenchmarkTests.cs` (lines 96, 104, 105, 123, 131, 132) — 6 issues

- [x] 10. Fix CA1861 w `TopicRegistryTests.cs` (lines 51, 52) — 2 issues

**Acceptance Criteria (per task)**:
- [ ] `CA1861` nie pojawia się w SonarQube dla tych plików
- [ ] Build przechodzi, testy przechodzą

### Wave 3 — CA1822 (1 production file)

- [x] 11. Fix CA1822 in `PrivacySanitizer.cs` (line 24)

  **What to do**: Dodaj `static` do metody `Sanitize` w `PrivacySanitizer.cs`.
  **File**: `src/HybridTherapist.Security/Privacy/PrivacySanitizer.cs`
  **Agent Profile**: `quick`

  **Note**: Upewnij się, że wszystkie testy dalej przechodzą — metoda `Sanitize` jest wołana z `TherapistFlow`.

---

## Commit Strategy

| # | Commit Message | Files |
|---|---------------|-------|
| 1 | `fix: resolve xUnit1042 — use TheoryData<> in test MemberData methods` | HandBenchmarkTests.cs, HandJsonBenchmarkTests.cs, HandPlaintextBenchmarkTests.cs, HandSemanticBenchmarkTests.cs, HandCheckpointLibraryTests.cs |
| 2 | `fix: resolve CA1861 — use static readonly arrays in test files` | HandBenchmarkValidator.cs, HandJsonBenchmarkTests.cs, HandPlaintextBenchmarkTests.cs, HandSemanticBenchmarkTests.cs, TopicRegistryTests.cs |
| 3 | `fix: resolve CA1822 — mark PrivacySanitizer.Sanitize as static` | PrivacySanitizer.cs |

Pre-commit: `dotnet build HybridTherapist.sln -c Release --nologo -v q && dotnet test tests/HybridTherapist.Tests -c Release --nologo`

---

## Success Criteria

```bash
# SonarQube: 0 issues
sonarqube_search_sonar_issues_in_projects --projects hybrid-therapist --statuses OPEN,CONFIRMED
# Expected: 0 results

# Build
dotnet build HybridTherapist.sln -c Release --nologo -v q
# Expected: 0 warnings, 0 errors

# Tests
dotnet test tests/HybridTherapist.Tests -c Release --nologo
# Expected: 280 passed, 0 failed
```
