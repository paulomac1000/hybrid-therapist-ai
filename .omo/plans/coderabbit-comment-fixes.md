# CodeRabbit Comment Fixes — PR #11

## TL;DR

> **Quick Summary**: 12 lingering CodeRabbit review comments — 8 MAJOR, 4 MINOR. All are quick-wins (1-3 lines each). Fix in a single commit.
>
> **Deliverables**:
> - 12 fixed code locations across 8 files
> - 0 remaining CodeRabbit threads on PR #11
>
> **Estimated Effort**: Quick
> **Parallel Execution**: YES — 4 independent waves
> **Critical Path**: Fix ChatEndpoints logging → commit

---

## Context

### Original Request
PR #11 ma 14 komentarzy CodeRabbit. 2 zostały automatycznie rozwiązane przez CodeRabbita ("Addressed in commits"), 12 pozostaje otwartych.

### CodeRabbit Thread References
| REST ID | GraphQL Thread ID | File | Severity |
|---|---|---|---|
| 3359644004 | `PRRT_kwDOSl0efc6HOuDk` | `ChatEndpoints.cs:72` | 🟠 MAJOR |
| 3359644010 | `PRRT_kwDOSl0efc6HOuDm` | `ResponseStrategySelector.cs:23` | 🟠 MAJOR |
| 3359644019 | `PRRT_kwDOSl0efc6HOuDr` | `HandJsonBenchmarkValidator.cs:147` | 🟠 MAJOR |
| 3359644023 | `PRRT_kwDOSl0efc6HOuDu` | `HandJsonBenchmarkValidator.cs:274` | 🟠 MAJOR |
| 3359644026 | `PRRT_kwDOSl0efc6HOuDx` | `HandSemanticBenchmarkValidator.cs:130` | 🟠 MAJOR |
| 3367935929 | `PRRT_kwDOSl0efc6HlxCG` | `dump-to-md.sh:35` | 🟠 MAJOR |
| 3367958748 | `PRRT_kwDOSl0efc6Hl1IW` | `.omo/plans/dockerfile-codec-fix.md` | 🟠 MAJOR |
| 3367958749 | `PRRT_kwDOSl0efc6Hl1IX` | `.omo/plans/docs-audit-fix.md` | 🟠 MAJOR |
| 3367958750 | `PRRT_kwDOSl0efc6Hl1IY` | `.omo/plans/dotnet-upgrade-8-to-10.md` | 🟠 MAJOR |
| 3367935936 | `PRRT_kwDOSl0efc6HlxCM` | `run-hand-benchmark.sh:231` | 🟡 MINOR |
| 3367935942 | `PRRT_kwDOSl0efc6HlxCS` | `run-hand-benchmark.sh:312` | 🟡 MINOR |
| 3359644024 | `PRRT_kwDOSl0efc6HOuDv` | `HandPlaintextBenchmarkValidator.cs:115` | 🟡 MINOR |

### Already Resolved
- 3359643990: `hand-semantic.md` hardcoded ~24% — CodeRabbit auto-closed ✅
- 3359644007: `TherapistLayerService.cs` pragma restore — CodeRabbit auto-closed ✅

---

## Work Objectives

### Core Objective
Fix all 12 remaining CodeRabbit review comments on PR #11, then resolve threads via GraphQL API.

### Concrete Deliverables
- 8 modified files with 1-3 line changes each
- GraphQL thread resolution for all 12 threads
- Build: 0W/0E, Tests: 280/280, Precommit: all checks pass

### Definition of Done
- [x] `dotnet build HybridTherapist.sln -c Release` → 0W/0E
- [x] `dotnet test tests/HybridTherapist.Tests -c Release --no-build` → 280/280
- [x] `gh api graphql` confirms all 12 threads `isResolved: true`
- [x] CI passes

### Must Have
- Logging fix: body content NEVER in production logs
- Null guard on `phase.ToUpperInvariant()`
- `AsyncLocal` restore semantics in both validators
- `min_quality_score` enforced in validation
- Shell operator precedence fix
- AFDS frontmatter on 3 plan files

### Must NOT Have
- Refactoring beyond the scope of each individual comment
- New abstractions or interfaces
- Changes to production behavior (except logging privacy)
- Stale comments left unresolved

---

## Verification Strategy

### Test Decision
- **Automated tests**: None (all fixes are in test code, shell scripts, or plan docs — no production logic changes except logging)
- **Verification**: Build + unit test suite + manual AFDS check

### QA Policy
Every task verified via build/test or manual command.

---

## Execution Strategy

### Parallel Execution Waves

```
Wave 1 (Start Immediately — security + null safety):
├── Task 1: ChatEndpoints.cs logging fix [quick]
├── Task 2: ResponseStrategySelector.cs null guard [quick]
├── Task 3: dump-to-md.sh operator precedence [quick]
└── Task 4: run-hand-benchmark.sh unused vars [quick]

Wave 2 (After Wave 1 — StrictCodecG fixes, MAX PARALLEL):
├── Task 5: HandJsonBenchmarkValidator.cs SaveAndRestore [quick]
└── Task 6: HandSemanticBenchmarkValidator.cs SaveAndRestore [quick]

Wave 3 (After Wave 2 — quality score + magic number):
├── Task 7: HandJsonBenchmarkValidator.cs MinQualityScore enforcement [quick]
└── Task 8: HandPlaintextBenchmarkValidator.cs named constant [quick]

Wave 4 (After Wave 3 — AFDS + resolution):
├── Task 9: AFDS frontmatter (3 files) [quick]
└── Task 10: Resolve all 12 threads via GraphQL [quick]

Critical Path: Task 1 → Task 5 → Task 7 → commit → Task 10
Parallel Speedup: ~60% faster than sequential
Max Concurrent: 4 (Wave 1)
```

---

## TODOs

- [x] 1. Fix ChatEndpoints.cs logging — redact body content

  **What to do**:
  - In `ChatEndpoints.cs` line 71-72, replace `rawBody.Length > 300 ? rawBody[..300] + "..." : rawBody` with `"[REDACTED — user content]"` or log only `rawBody.Length` without content
  - Keep the exception `ex` attached so stack trace is preserved

  **Must NOT do**:
  - Don't change any other logging calls
  - Don't remove the exception parameter

  **Recommended Agent Profile**: `quick`
  - Single-line fix, no test changes needed

  **Acceptance Criteria**:
  - [ ] `dotnet build HybridTherapist.sln -c Release` → 0W/0E
  - [ ] No `rawBody[..` substring in `ChatEndpoints.cs` after fix

  **Commit**: YES
  - Files: `src/HybridTherapist.Api/Endpoints/ChatEndpoints.cs`

- [x] 2. Fix ResponseStrategySelector.cs null guard

  **What to do**:
  - In `ResponseStrategySelector.cs` line 23, add null/empty guard before `phase.ToUpperInvariant()`:
  ```csharp
  private static ResponseStrategy MapStrategy(string phase, bool high, bool moderate)
  {
      if (string.IsNullOrWhiteSpace(phase))
          return ResponseStrategy.Intake;
      return phase.ToUpperInvariant() switch
      ...
  }
  ```

  **Must NOT do**:
  - Don't change the switch cases or `Resolve` logic

  **Recommended Agent Profile**: `quick`
  - Single-line guard, no test changes needed (existing tests cover non-null paths)

  **Acceptance Criteria**:
  - [ ] `dotnet build HybridTherapist.sln -c Release` → 0W/0E

  **Commit**: YES (groups with 1)
  - Files: `src/HybridTherapist.Domain/Services/ResponseStrategySelector.cs`

- [x] 3. Fix dump-to-md.sh operator precedence

  **What to do**:
  - In `dump-to-md.sh` line 35, wrap the OR condition:
  ```diff
  -    [ "$f" = "$SCRIPT_NAME" ] || [ "$f" = "$OUTPUT_NAME" ] && continue
  +    { [ "$f" = "$SCRIPT_NAME" ] || [ "$f" = "$OUTPUT_NAME" ]; } && continue
  ```

  **Must NOT do**:
  - Don't change the while loop structure

  **Recommended Agent Profile**: `quick`
  - One character change (`{ ...; }`)

  **Acceptance Criteria**:
  - [ ] `bash -n dump-to-md.sh` passes syntax check
  - [ ] Script still functions correctly

  **Commit**: YES (groups with 1)
  - Files: `dump-to-md.sh`

- [x] 4. Fix run-hand-benchmark.sh unused variables

  **What to do**:
  - Line 230-231: Remove `token_count_json` declaration + assignment (or add `"count": $token_count_json` to the JSON output at line 245)
  - Line 312: Remove `LIVE_STATUS` assignment (unused)
  - Decision: Remove both unused variables (cleaner than adding unused JSON fields)

  **Must NOT do**:
  - Don't change JSON structure

  **Recommended Agent Profile**: `quick`
  - Two-line removal

  **Acceptance Criteria**:
  - [ ] `bash -n scripts/run-hand-benchmark.sh` passes
  - [ ] `shellcheck scripts/run-hand-benchmark.sh` → no SC2034 warnings

  **Commit**: YES (groups with 1)
  - Files: `scripts/run-hand-benchmark.sh`

- [x] 5. Fix HandJsonBenchmarkValidator.cs StrictCodecG restore

  **What to do**:
  - In `HandJsonBenchmarkValidator.cs` lines 132-147, save original value before setting:
  ```csharp
  bool originalStrictMode = TokenSavingsTracker.StrictCodecG;
  TokenSavingsTracker.StrictCodecG = true;
  try { ... }
  finally { TokenSavingsTracker.StrictCodecG = originalStrictMode; }
  ```

  **Must NOT do**:
  - Don't add locks (this is test code, run sequentially)
  - Don't change the calculation logic

  **Recommended Agent Profile**: `quick`

  **Acceptance Criteria**:
  - [ ] `dotnet build HybridTherapist.sln -c Release` → 0W/0E

  **Commit**: YES (groups with 6)
  - Files: `tests/HybridTherapist.Integration/HandJsonBenchmarkValidator.cs`

- [x] 6. Fix HandSemanticBenchmarkValidator.cs StrictCodecG restore

  **What to do**:
  - Same fix as Task 5 — save original before setting, restore in finally
  - In `HandSemanticBenchmarkValidator.cs` lines 116-131

  **Must NOT do**:
  - Don't add locks

  **Recommended Agent Profile**: `quick`

  **Acceptance Criteria**:
  - [ ] `dotnet build HybridTherapist.sln -c Release` → 0W/0E

  **Commit**: YES (groups with 5)
  - Files: `tests/HybridTherapist.Integration/HandSemanticBenchmarkValidator.cs`

- [x] 7. Fix HandJsonBenchmarkValidator.cs MinQualityScore enforcement

  **What to do**:
  - In `HandJsonBenchmarkValidator.cs` line 253-274, add quality score check in `ValidateExpectedQuality`:
  ```csharp
  // After existing checks, before closing brace:
  if (expected.MinQualityScore.HasValue)
      run.QualityScore.Should().BeGreaterOrEqualTo(
          expected.MinQualityScore.Value,
          $"quality score must be ≥ {expected.MinQualityScore.Value}");
  ```

  **Must NOT do**:
  - Don't change `BenchmarkExpectations` class

  **Recommended Agent Profile**: `quick`
  - ~3 lines, straightforward FluentAssertions check

  **Acceptance Criteria**:
  - [ ] `dotnet build HybridTherapist.sln -c Release` → 0W/0E
  - [ ] `dotnet test tests/HybridTherapist.Tests` → all pass

  **Commit**: YES (groups with 8)
  - Files: `tests/HybridTherapist.Integration/HandJsonBenchmarkValidator.cs`

- [x] 8. Fix HandPlaintextBenchmarkValidator.cs magic number

  **What to do**:
  - In `HandPlaintextBenchmarkValidator.cs` line 114-115, extract constant:
  ```csharp
  // Theoretical Compact wire size baseline (~35 tokens) derived from
  // L2 + L3 Codec G memos in benchmark cassettes at docs/benchmarks/hand-compact.md
  private const int CompactTokensBaseline = 35;
  ```

  **Must NOT do**:
  - Don't change the value 35

  **Recommended Agent Profile**: `quick`
  - One `const` declaration

  **Acceptance Criteria**:
  - [ ] `dotnet build HybridTherapist.sln -c Release` → 0W/0E
  - [ ] `dotnet test tests/HybridTherapist.Tests` → all pass

  **Commit**: YES (groups with 7)
  - Files: `tests/HybridTherapist.Integration/HandPlaintextBenchmarkValidator.cs`

- [x] 9. Add AFDS YAML frontmatter to 3 plan files

  **What to do**:
  - Prepend AFDS frontmatter to each file using the project standard schema from `docs/architecture.md`:
  ```yaml
  ---
  description: [file-specific summary]
  doc_id: plan.[slug]
  type: plan
  status: complete
  rigor_tier: L3
  ttl_days: 30
  stability: stable
  ai_scope: editable
  source_of_truth: false
  ---
  ```

  **Files**:
  - `.omo/plans/dockerfile-codec-fix.md` — `doc_id: plan.dockerfile-codec-fix`
  - `.omo/plans/docs-audit-fix.md` — `doc_id: plan.docs-audit-fix`
  - `.omo/plans/dotnet-upgrade-8-to-10.md` — `doc_id: plan.dotnet-upgrade`

  **Must NOT do**:
  - Don't modify any content below the `---` line

  **Recommended Agent Profile**: `quick`
  - 3 identical operations on 3 files

  **Acceptance Criteria**:
  - [ ] AFDS validate-docs passes in CI
  - [ ] Each file has exactly one YAML frontmatter block

  **Commit**: YES
  - Files: `.omo/plans/dockerfile-codec-fix.md`, `.omo/plans/docs-audit-fix.md`, `.omo/plans/dotnet-upgrade-8-to-10.md`

- [x] 10. Resolve all 12 GraphQL threads via API

  **What to do**:
  - Using the `resolveReviewThread` mutation for each `PRRT_` ID from the table above
  - Resolve threads in order: already-fixed topics first, then confirmed-fixed after build passes

  **GraphQL IDs to resolve**:
  ```
  PRRT_kwDOSl0efc6HOuDk  (ChatEndpoints logging)
  PRRT_kwDOSl0efc6HOuDm  (null phase)
  PRRT_kwDOSl0efc6HOuDr  (StrictCodecG JSON)
  PRRT_kwDOSl0efc6HOuDu  (min_quality_score)
  PRRT_kwDOSl0efc6HOuDx  (StrictCodecG Semantic)
  PRRT_kwDOSl0efc6HOuDv  (magic number)
  PRRT_kwDOSl0efc6HlxCG  (dump-to-md.sh)
  PRRT_kwDOSl0efc6HlxCM  (token_count_json)
  PRRT_kwDOSl0efc6HlxCS  (LIVE_STATUS)
  PRRT_kwDOSl0efc6Hl1IW  (AFDS dockerfile-codec-fix)
  PRRT_kwDOSl0efc6Hl1IX  (AFDS docs-audit-fix)
  PRRT_kwDOSl0efc6Hl1IY  (AFDS dotnet-upgrade)
  ```

  **Recommended Agent Profile**: `quick`

  **Acceptance Criteria**:
  - [ ] All 12 threads show `isResolved: true`
  - [ ] PR #11 shows 0 unresolved CodeRabbit comments

  **Commit**: NO (API mutation, not code change)

---

## Final Verification Wave (MANDATORY — after ALL implementation tasks)

- [x] F1. **Build Check** — `dotnet build HybridTherapist.sln -c Release --nologo`
  Output: Build succeeded. 0 Warning(s) 0 Error(s) | VERDICT: PASS

- [x] F2. **Unit Tests** — `dotnet test tests/HybridTherapist.Tests -c Release --no-build`
  Output: 280/280 pass | VERDICT: PASS

- [x] F3. **Precommit Hook** — run full hook: restore → format → build → test → semgrep → AFDS
  Output: ALL CHECKS PASSED | VERDICT: PASS

- [x] F4. **Thread Resolution Check** — verify all 12 GraphQL threads `isResolved: true`
  Output: 12/12 resolved | VERDICT: PASS

---

## Commit Strategy

- **Commit 1**: `fix(coderabbit): ChatEndpoints body leak, null guard, shell precedence, unused vars`
  Files: `ChatEndpoints.cs`, `ResponseStrategySelector.cs`, `dump-to-md.sh`, `run-hand-benchmark.sh`
- **Commit 2**: `fix(coderabbit): StrictCodecG AsyncLocal restore in validators`
  Files: `HandJsonBenchmarkValidator.cs`, `HandSemanticBenchmarkValidator.cs`
- **Commit 3**: `fix(coderabbit): enforce MinQualityScore, extract CompactTokens constant`
  Files: `HandJsonBenchmarkValidator.cs`, `HandPlaintextBenchmarkValidator.cs`
- **Commit 4**: `fix(coderabbit): add AFDS YAML frontmatter to 3 plan files`
  Files: `.omo/plans/dockerfile-codec-fix.md`, `.omo/plans/docs-audit-fix.md`, `.omo/plans/dotnet-upgrade-8-to-10.md`
- **Post-push**: Resolve all 12 threads via GraphQL API

---

## Success Criteria

### Verification Commands
```bash
dotnet build HybridTherapist.sln -c Release --nologo -v q  # Expected: Build succeeded. 0W/0E
dotnet test tests/HybridTherapist.Tests -c Release --no-build  # Expected: 280/280
bash -n dump-to-md.sh  # Expected: no output (valid syntax)
```

### Final Checklist
- [x] Build: 0W/0E
- [x] Unit tests: 280/280 pass
- [x] Precommit: all 6 checks pass
- [x] CodeRabbit: 12/12 threads resolved
- [x] PR #11: 0 unresolved comments
