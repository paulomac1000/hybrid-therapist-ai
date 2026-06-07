---
description: Fix 7 documentation issues from post-HandCodec v0.3.0 audit — resilience ladder, benchmarks, cassettes, dates
doc_id: plan.docs-audit-fix
type: plan
status: complete
rigor_tier: L3
ttl_days: 30
stability: stable
ai_scope: editable
source_of_truth: false
---

# Documentation Audit Fix — v0.4.0 Post-Upgrade Cleanup

## TL;DR

> **Quick Summary**: Fix 7 documentation issues from a post-HandCodec v0.3.0 audit: update resilience ladder 5→6 levels, regenerate benchmark data from TRX, translate cassettes README, refresh stale dates, regenerate health-report via CI, add explanatory comments, fill doc-registry gaps.
>
> **Deliverables**:
> - 3 docs updated: resilience ladder 5→6 (socrates-pipeline.md, architecture.md, glossary.md)
> - 3 benchmark docs regenerated from fresh TRX run (hand-semantic.md, hand-json.md, plaintext.md)
> - 4 artifact files regenerated (hand-*-latest.md)
> - 1 file translated PL→EN (cassettes/README.md)
> - 8 docs: `last_verified` dates bumped to 2026-06-06
> - 1 CI-regenerated health-report.md
> - 2 explanatory comments added (architecture.md, socrates-pipeline.md)
> - 1 doc-registry.md: 4 missing entries added
>
> **Estimated Effort**: Short (~14 markdown edits + 1 benchmark run)
> **Parallel Execution**: YES — 3 waves
> **Critical Path**: Wave 1 (resilience verification) → Wave 2 (benchmark run) → Wave 3 (all remaining fixes)

---

## Context

### Original Request
"wykonaj głęboką analizę dokumentacji, oceń czy wszystko z nią jest OK" → "przygotuj plan naprawczy"

### Interview Summary
**Key Discussions**:
- Conducted full audit of 22+ markdown files across docs/, artifacts/, tests/, and root
- Post-upgrade drift: HandCodec v0.3.0 expanded resilience ladder 5→6, but 3 docs still describe 5 levels
- Benchmark docs contain manually-entered numbers that contradict TRX data
- Cassettes README is in Polish while all others are in English
- Two audit findings were false positives: `[ANALYST CONTEXT]` (matches QualityValidator code) and `S=crisis` (used in test data) — both are correct as-is

**User Decisions**:
- Benchmark data: run fresh from scratch (`run-hand-benchmark.sh --cassette --all-variants --report`)
- Cassettes README: translate to English
- health-report.md: keep in plan, regenerate via CI
- False positives: add explanatory comments (not remove)

### Metis Review
**Identified Gaps** (addressed):
- Resilience ladder levels must be verified against actual HandCodec v0.3.0 API before fixing docs
- `[ANALYST CONTEXT]` and `S=crisis` verified as intentional — withdrawn from audit, comments added instead
- TRX file validity confirmed — all 4 variant TRX files contain `BENCHMARK_TOKEN_SAVINGS=` markers
- health-report.md is CI-generated — plan includes CI pipeline run, not manual edit

---

## Work Objectives

### Core Objective
Bring all documentation into consistency with HandCodec v0.3.0 reality and regenerate all stale benchmark data from canonical TRX source.

### Concrete Deliverables
- 3 docs: resilience ladder table updated from 5 to 6 levels
- 3 docs + 4 artifacts: benchmark numbers rewritten from fresh TRX data
- 1 file: cassettes/README.md translated to English
- 8 docs: `last_verified` bumped to `2026-06-06`
- 1 file: health-report.md regenerated via CI
- 2 docs: explanatory comments added for false-positive audit items
- 1 file: doc-registry.md entries added for AGENTS.md, CHANGELOG.md, README.md, cassettes/README.md

### Definition of Done
- [ ] `grep "5.level.*resilience\|5-level.*degradation" docs/*.md docs/**/*.md` → zero matches
- [ ] `grep "6.level\|Level 6.*passthrough" docs/socrates-pipeline.md docs/architecture.md docs/meta/glossary.md` → ≥1 match each
- [ ] Benchmark numbers in docs/benchmarks/{hand-semantic,hand-json,plaintext}.md match TRX output (±0.1pp)
- [ ] `grep -c "[ąćęłńóśźż]" tests/HybridTherapist.Integration/Cassettes/README.md` → 0 (no Polish diacritics in body text)
- [ ] `grep "last_verified: 2026-06-06" docs/*.md docs/**/*.md` → ≥8 matches
- [ ] `dotnet build HybridTherapist.sln -c Release --nologo -v q` → 0 warnings, 0 errors

### Must Have
- Resilience ladder 5→6 in all 3 affected docs
- Benchmark data matching TRX in all docs + artifacts
- Cassettes README in English
- `last_verified` dates updated
- health-report.md regenerated via CI (or manual if CI unavailable)

### Must NOT Have (Guardrails)
- Do NOT touch CHANGELOG.md (historical record, correctly documents the 5→6 upgrade)
- Do NOT touch layer-necessity.md or any code files
- Do NOT manually edit health-report.md line counts — regenerate via CI
- Do NOT remove `[ANALYST CONTEXT]` or `S=crisis` references — they're correct
- Do NOT reorganize the docs/ folder structure
- Do NOT modify README.md

---

## Verification Strategy (MANDATORY)

> **ZERO HUMAN INTERVENTION** — ALL verification is agent-executed.

### Test Decision
- **Infrastructure exists**: YES (build, grep, TRX parsing)
- **Automated tests**: None — documentation-only task
- **Agent-Executed QA**: Manual verification via grep + build + TRX parsing

### QA Policy
- **Doc correctness**: `grep` assertions on exact strings (resilience level counts, last_verified dates, Polish diacritics)
- **Benchmark integrity**: Parse TRX `BENCHMARK_TOKEN_SAVINGS=` values, compare against doc tables
- **Build integrity**: `dotnet build` after all changes to verify no broken cross-references
- **Translation quality**: Agent reviews cassettes/README.md for natural English, no Polish diacritics, preserved technical terms

---

## Execution Strategy

### Parallel Execution Waves

```
Wave 1 (Foundation — verification + benchmark run):
├── Task 1: Verify HandCodec v0.3.0 resilience ladder API [deep]
├── Task 2: Run fresh benchmarks via script [quick]
└── Task 3: Parse TRX output, compute aggregates [quick]

Wave 2 (Critical fixes — MAX PARALLEL):
├── Task 4: Fix resilience ladder in socrates-pipeline.md [quick]
├── Task 5: Fix resilience ladder in architecture.md [quick]
├── Task 6: Fix resilience ladder in glossary.md [quick]
├── Task 7: Regenerate hand-semantic.md benchmark data [quick]
├── Task 8: Regenerate hand-json.md benchmark data [quick]
├── Task 9: Regenerate plaintext.md benchmark data [quick]
├── Task 10: Regenerate 4 artifact -latest.md files [quick]
└── Task 11: Translate cassettes/README.md to English [writing]

Wave 3 (Polish + registry — MAX PARALLEL):
├── Task 12: Bump last_verified dates in 8 docs [quick]
├── Task 13: Add explanatory comments (2 docs) [quick]
├── Task 14: Fill doc-registry.md missing entries [quick]
└── Task 15: Regenerate health-report.md via CI [quick]

Wave FINAL:
├── Task F1: Plan compliance audit (oracle)
├── Task F2: Build verification + grep assertions
├── Task F3: Manual QA — read all changed files
└── Task F4: Scope fidelity check (deep)
```

**Critical Path**: Task 1 → Task 2 → Task 3 → Tasks 4-11 → Tasks 12-15 → F1-F4
**Parallel Speedup**: ~60% faster than sequential (8 tasks in Wave 2, 4 in Wave 3)
**Max Concurrent**: 8 (Wave 2)

---

## TODOs

### Wave 1 — Foundation (verification + benchmark run)

- [x] 1. Verify HandCodec v0.3.0 resilience ladder API against CHANGELOG description

  **What to do**:
  - Read `CHANGELOG.md` lines 18-28 (HandCodec upgrade section) — confirm Level 5 = JSON extraction, Level 6 = passthrough
  - Grep `local-packages/` for HandCodec 0.3.0 .nupkg info (confirm version)
  - Check `AnalystLayer.cs:117` and `SupervisorLayer.cs:121` — the `parsed.Level >= 6` guards confirm Level 6 is the fallback boundary
  - Verify the correct level names: Level 5 = "JSON extraction" (opt-in), Level 6 = "passthrough/fallback"
  - Output: confirmed level numbering (1-6) and each level's description

  **Must NOT do**:
  - Do NOT read the external hand-codec repo — use local code references only (AnalystLayer, SupervisorLayer, CHANGELOG)
  - Do NOT modify any code files

  **Recommended Agent Profile**:
  - **Category**: `deep`
    - Reason: Requires cross-referencing CHANGELOG, source code guards, and package metadata
  - **Skills**: [`dotnet`]
    - `dotnet`: C# codebase navigation for AnalystLayer.cs and SupervisorLayer.cs

  **Parallelization**:
  - **Can Run In Parallel**: NO (blocks Task 2)
  - **Parallel Group**: Wave 1 (sequential prerequisite)
  - **Blocks**: Task 2, Tasks 4-6
  - **Blocked By**: None (can start immediately)

  **Acceptance Criteria**:
  - [ ] CHANGELOG Level 5/6 description confirmed against source code guards (`parsed.Level >= 6`)
  - [ ] Exact level names documented for use in Tasks 4-6

  **QA Scenarios**:

  ```
  Scenario: Code guards confirm Level 6 is fallback boundary
    Tool: Bash (grep)
    Steps:
      1. grep "parsed.Level >= 6" src/HybridTherapist.Application/Layers/AnalystLayer.cs
      2. grep "parsed.Level >= 6" src/HybridTherapist.Application/Layers/SupervisorLayer.cs
      3. grep "decoder_level6_passthrough" src/HybridTherapist.Application/Layers/AnalystLayer.cs
      4. grep "decoder_level6_passthrough" src/HybridTherapist.Application/Layers/SupervisorLayer.cs
    Expected Result: All 4 greps return matches — code uses Level 6 as passthrough boundary
    Failure Indicators: Any grep returns empty — code still uses Level 5, plan must be adjusted
    Evidence: .omo/evidence/task-1-resilience-guards.txt
  ```

  **Commit**: NO

---

- [x] 2. Run fresh benchmark suite (all 4 variants, cassette mode)

  **What to do**:
  - Execute: `./scripts/run-hand-benchmark.sh --cassette --all-variants --report`
  - This runs compact, semantic, plaintext, json, and checkpoint variants
  - Generates new TRX files in `artifacts/benchmarks/`
  - Generates new `-latest.md` and `-latest.json` artifact files
  - Expected duration: ~2-5 minutes (cassette mode, no Docker/Ollama)

  **Must NOT do**:
  - Do NOT run `--live` mode (requires Docker + Ollama)
  - Do NOT modify the benchmark script

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Single command execution with well-defined shell script

  **Parallelization**:
  - **Can Run In Parallel**: NO (blocks Task 3, requires Task 1 completion)
  - **Parallel Group**: Wave 1 (sequential prerequisite)
  - **Blocks**: Tasks 3, 7-10
  - **Blocked By**: Task 1

  **Acceptance Criteria**:
  - [ ] Script exits with code 0
  - [ ] All 4 variant TRX files exist: `hand-compact-benchmark.trx`, `hand-semantic-benchmark.trx`, `hand-json-benchmark.trx`, `hand-plaintext-benchmark.trx`
  - [ ] All 4 `-latest.md` artifact files updated with new timestamps

  **QA Scenarios**:

  ```
  Scenario: Benchmark script completes successfully
    Tool: Bash
    Steps:
      1. cd /home/pablo/Projects/hybrid-therapist
      2. ./scripts/run-hand-benchmark.sh --cassette --all-variants --report
      3. echo $?
    Expected Result: Exit code 0, output contains "All benchmarks passed"
    Failure Indicators: Non-zero exit code, output contains "FAILED" or error messages
    Evidence: .omo/evidence/task-2-benchmark-output.txt

  Scenario: All expected TRX files generated
    Tool: Bash
    Steps:
      1. ls -la artifacts/benchmarks/hand-compact-benchmark.trx
      2. ls -la artifacts/benchmarks/hand-semantic-benchmark.trx
      3. ls -la artifacts/benchmarks/hand-json-benchmark.trx
      4. ls -la artifacts/benchmarks/hand-plaintext-benchmark.trx
    Expected Result: All 4 files exist with recent modification timestamps
    Failure Indicators: Any file missing or with old timestamp
    Evidence: .omo/evidence/task-2-trx-files.txt
  ```

  **Commit**: NO (artifact regeneration committed in Task 10)

---

- [x] 3. Parse TRX output, compute benchmark aggregates for each variant

  **What to do**:
  - For each variant TRX file, grep `BENCHMARK_TOKEN_SAVINGS=` lines
  - Compute: count of values, arithmetic mean (avg %), min %, max %
  - Record per-variant aggregates for Tasks 7-9
  - Verify aggregates are non-null and make sense (Compact > 15%, Semantic > 0%, Plaintext < 0%)

  **Must NOT do**:
  - Do NOT hardcode numbers — compute from actual TRX output
  - Do NOT use old TRX files — must be from Task 2's fresh run

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Simple data extraction and arithmetic from known file format

  **Parallelization**:
  - **Can Run In Parallel**: NO (depends on Task 2 output)
  - **Parallel Group**: Wave 1 (sequential prerequisite)
  - **Blocks**: Tasks 7-9
  - **Blocked By**: Task 2

  **Acceptance Criteria**:
  - [ ] 4 sets of aggregates computed: Compact, Semantic, JSON, Plaintext
  - [ ] Compact avg > 15% (matches CHANGELOG assertion)
  - [ ] Semantic avg > 0% (matches CHANGELOG assertion)
  - [ ] Plaintext avg < 0% (matches CHANGELOG assertion)
  - [ ] All values stored for use in Tasks 7-9

  **QA Scenarios**:

  ```
  Scenario: TRX files contain BENCHMARK_TOKEN_SAVINGS markers
    Tool: Bash
    Steps:
      1. grep -c "BENCHMARK_TOKEN_SAVINGS" artifacts/benchmarks/hand-semantic-benchmark.trx
      2. grep -c "BENCHMARK_TOKEN_SAVINGS" artifacts/benchmarks/hand-json-benchmark.trx
      3. grep -c "BENCHMARK_TOKEN_SAVINGS" artifacts/benchmarks/hand-plaintext-benchmark.trx
    Expected Result: All counts > 0
    Failure Indicators: Any TRX has 0 markers — benchmark run failed silently
    Evidence: .omo/evidence/task-3-trx-aggregates.txt

  Scenario: Computed aggregates pass sanity checks
    Tool: Bash (awk/bc)
    Steps:
      1. Compute avg for hand-semantic-benchmark.trx
      2. Compute avg for hand-plaintext-benchmark.trx
      3. Assert: Semantic avg > 0 AND Plaintext avg < 0
    Expected Result: Semantic positive, Plaintext negative
    Failure Indicators: Both positive or both negative — data anomaly
    Evidence: .omo/evidence/task-3-aggregate-sanity.txt
  ```

  **Commit**: NO

---

### Wave 2 — Critical fixes (MAX PARALLEL — all 8 can run simultaneously after Wave 1)

- [x] 4. Fix resilience ladder in docs/socrates-pipeline.md — 5→6 levels

  **What to do**:
  - Line 22: `"the 5-level resilience ladder"` → `"the 6-level resilience ladder"`
  - Lines 69-80: Replace 5-row resilience ladder table with 6-row version:
    - Level 1: Strict — Perfect format
    - Level 2: Lenient — Minor format deviations repaired
    - Level 3: Markdown Strip — Wire in ``` fences stripped
    - Level 4: Semantic Extraction — Regex extracts from prose
    - Level 5: JSON Extraction — Attempts JSON parse (new in v0.3.0)
    - Level 6: Passthrough/Fallback — Safe replacement memo
  - Line 80: `"Level 5 (unstructured passthrough)"` → `"Level 6 (passthrough/fallback)"`

  **Must NOT do**:
  - Do NOT add S=crisis explanation here — that's Task 13
  - Do NOT change any other content in this file

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Precise, known line-level text replacements in a single file

  **Parallelization**:
  - **Can Run In Parallel**: YES (with Tasks 5-11)
  - **Parallel Group**: Wave 2
  - **Blocks**: None
  - **Blocked By**: Task 1 (resilience ladder verified)

  **Acceptance Criteria**:
  - [ ] `grep "5.level.*resilience\|5-level.*degradation" docs/socrates-pipeline.md` → zero matches
  - [ ] `grep "6.level.*resilience" docs/socrates-pipeline.md` → 1 match
  - [ ] Resilience table has exactly 6 rows (Level 1 through Level 6)
  - [ ] Level 5 row contains "JSON"
  - [ ] Level 6 row contains "passthrough" or "fallback"

  **QA Scenarios**:

  ```
  Scenario: All 5-level references purged
    Tool: Bash (grep)
    Steps:
      1. grep -n "5.level\|5 level\|five.level\|Level 5.*passthrough\|Level 5.*unstructured" docs/socrates-pipeline.md
    Expected Result: Zero matches
    Failure Indicators: Any match — incomplete fix
    Evidence: .omo/evidence/task-4-purge.txt

  Scenario: 6-level table verified
    Tool: Bash (grep)
    Steps:
      1. grep -c "| [1-6] |" docs/socrates-pipeline.md
    Expected Result: ≥6 (table has Level 1-6 rows)
    Failure Indicators: <6 — table incomplete
    Evidence: .omo/evidence/task-4-table-rows.txt
  ```

  **Commit**: YES
  - Message: `docs: fix resilience ladder 5→6 in socrates-pipeline.md (HandCodec v0.3.0)`
  - Files: `docs/socrates-pipeline.md`

---

- [x] 5. Fix resilience ladder in docs/architecture.md — 5→6 levels

  **What to do**:
  - Line 199: `"HandResiliencePipeline.Parse() (levels 1-5)"` → `"(levels 1-6)"`
  - Line 201: `"Level 5 (unstructured passthrough) triggers"` → `"Level 6 (passthrough) triggers"`
  - Line 239: `"Level 5 triggers a safe fallback memo"` → `"Level 6 triggers a safe fallback memo"`
  - Line 240: `"decoder_level5_fallback"` → `"decoder_level6_passthrough"`

  **Must NOT do**:
  - Do NOT change the Resilience Ladder section header or structure
  - Do NOT touch the data flow table or layer descriptions

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: 4 precise line-level string replacements in a single file

  **Parallelization**:
  - **Can Run In Parallel**: YES (with Tasks 4, 6-11)
  - **Parallel Group**: Wave 2
  - **Blocks**: None
  - **Blocked By**: Task 1

  **Acceptance Criteria**:
  - [ ] `grep "Level 5.*passthrough\|Level 5.*fallback\|levels 1-5\|decoder_level5_fallback" docs/architecture.md` → zero matches
  - [ ] `grep "levels 1-6\|Level 6.*passthrough\|decoder_level6_passthrough" docs/architecture.md` → ≥3 matches

  **QA Scenarios**:

  ```
  Scenario: Old level-5 references purged
    Tool: Bash (grep)
    Steps:
      1. grep -n "Level 5.*passthrough\|Level 5.*fallback\|levels 1-5\|decoder_level5" docs/architecture.md
    Expected Result: Zero matches
    Failure Indicators: Any match — fix incomplete
    Evidence: .omo/evidence/task-5-purge.txt

  Scenario: New level-6 references present
    Tool: Bash (grep)
    Steps:
      1. grep -c "levels 1-6\|Level 6.*passthrough\|decoder_level6_passthrough" docs/architecture.md
    Expected Result: ≥3
    Failure Indicators: <3 — some occurrences missed
    Evidence: .omo/evidence/task-6-present.txt
  ```

  **Commit**: YES
  - Message: `docs: fix resilience ladder 5→6 in architecture.md (HandCodec v0.3.0)`
  - Files: `docs/architecture.md`

---

- [x] 6. Fix resilience ladder in docs/meta/glossary.md — 5→6 levels

  **What to do**:
  - Line 25: `"5-level degradation pipeline"` → `"6-level degradation pipeline (strict → lenient → markdown_strip → semantic → json_extraction → unstructured)"`

  **Must NOT do**:
  - Do NOT expand the glossary entry beyond the one-line fix

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Single line replacement in a small file

  **Parallelization**:
  - **Can Run In Parallel**: YES (with Tasks 4-5, 7-11)
  - **Parallel Group**: Wave 2
  - **Blocks**: None
  - **Blocked By**: Task 1

  **Acceptance Criteria**:
  - [ ] `grep "5-level degradation" docs/meta/glossary.md` → zero matches
  - [ ] `grep "6-level degradation" docs/meta/glossary.md` → 1 match

  **QA Scenarios**:

  ```
  Scenario: Glossary entry updated
    Tool: Bash (grep)
    Steps:
      1. grep "6-level degradation pipeline" docs/meta/glossary.md
    Expected Result: 1 match containing the full 6-level description
    Failure Indicators: Empty or still says "5-level"
    Evidence: .omo/evidence/task-6-glossary.txt
  ```

  **Commit**: YES (groups with Tasks 4-5)
  - Message: `docs: fix resilience ladder 5→6 in glossary.md (HandCodec v0.3.0)`
  - Files: `docs/meta/glossary.md`

---

- [x] 7. Regenerate docs/benchmarks/hand-semantic.md with TRX data

  **What to do**:
  - Replace the hardcoded results table (lines 42-50) with actual TRX data from Task 3
  - Update: "Average token savings: ~4.4%" → actual computed average from TRX (expected ~37%)
  - Update scenario-level rows: anxiety, depression, insomnia → actual values
  - Update the NOTE callout to reflect actual (higher) savings vs Compact
  - Update "Last run" date to current date

  **Must NOT do**:
  - Do NOT change the document structure (Purpose, Key Mapping, Interpretation sections)
  - Do NOT fabricate numbers — use ONLY Task 3's computed aggregates

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Table replacement using pre-computed data from Task 3

  **Parallelization**:
  - **Can Run In Parallel**: YES (with Tasks 4-6, 8-11)
  - **Parallel Group**: Wave 2
  - **Blocks**: None
  - **Blocked By**: Task 3 (TRX aggregates)

  **Acceptance Criteria**:
  - [ ] Results table scenario values match TRX (±0.1pp per scenario)
  - [ ] Average savings value matches Task 3's computed mean
  - [ ] "Last run" date is current date

  **QA Scenarios**:

  ```
  Scenario: Table values match TRX
    Tool: Bash (grep + compare)
    Steps:
      1. grep "Average token savings" docs/benchmarks/hand-semantic.md
      2. Compare against Task 3's semantic aggregate
    Expected Result: Value matches TRX aggregate ±0.1pp
    Failure Indicators: Value differs significantly from TRX
    Evidence: .omo/evidence/task-7-semantic-values.txt
  ```

  **Commit**: YES (groups with Tasks 8-10)
  - Message: `docs: regenerate hand-semantic benchmark data from TRX`
  - Files: `docs/benchmarks/hand-semantic.md`

---

- [x] 8. Regenerate docs/benchmarks/hand-json.md with TRX data

  **What to do**:
  - Replace the hardcoded results table (lines 35-41) with actual TRX data from Task 3
  - Update: "Average token savings: ~6.8%" → actual computed average from TRX
  - Update scenario-level rows: anxiety, depression, insomnia → actual values
  - Update "Last run" date

  **Must NOT do**:
  - Do NOT change document structure or JSON examples

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Table replacement using pre-computed data

  **Parallelization**:
  - **Can Run In Parallel**: YES (with Tasks 4-7, 9-11)
  - **Parallel Group**: Wave 2
  - **Blocks**: None
  - **Blocked By**: Task 3

  **Acceptance Criteria**:
  - [ ] Table values match TRX (±0.1pp per scenario)
  - [ ] Average savings value matches Task 3's computed mean

  **QA Scenarios**:

  ```
  Scenario: Table values match TRX
    Tool: Bash (grep + compare)
    Steps:
      1. grep "Average token savings" docs/benchmarks/hand-json.md
      2. Compare against Task 3's json aggregate
    Expected Result: Value matches TRX aggregate ±0.1pp
    Evidence: .omo/evidence/task-8-json-values.txt
  ```

  **Commit**: YES (groups with Tasks 7, 9-10)
  - Message: `docs: regenerate hand-json benchmark data from TRX`
  - Files: `docs/benchmarks/hand-json.md`

---

- [x] 9. Regenerate docs/benchmarks/plaintext.md with TRX data

  **What to do**:
  - Replace the hardcoded results table (lines 29-34) with actual TRX data from Task 3
  - Update: "Average token savings: ~-41.9%" → actual computed average from TRX (expected ~-147%)
  - Update scenario-level rows → actual values
  - Update the IMPORTANT callout with correct magnitude
  - Update "Last run" date

  **Must NOT do**:
  - Do NOT change document structure

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Table replacement using pre-computed data

  **Parallelization**:
  - **Can Run In Parallel**: YES (with Tasks 4-8, 10-11)
  - **Parallel Group**: Wave 2
  - **Blocks**: None
  - **Blocked By**: Task 3

  **Acceptance Criteria**:
  - [ ] Table values match TRX (±0.1pp per scenario)
  - [ ] Average savings value matches Task 3's computed mean
  - [ ] IMPORTANT callout reflects correct magnitude of overhead

  **QA Scenarios**:

  ```
  Scenario: Table values match TRX
    Tool: Bash (grep + compare)
    Steps:
      1. grep "Average token savings" docs/benchmarks/plaintext.md
      2. Compare against Task 3's plaintext aggregate
    Expected Result: Value matches TRX aggregate (±0.1pp), negative
    Evidence: .omo/evidence/task-9-plaintext-values.txt
  ```

  **Commit**: YES (groups with Tasks 7-8, 10)
  - Message: `docs: regenerate plaintext benchmark data from TRX`
  - Files: `docs/benchmarks/plaintext.md`

---

- [x] 10. Regenerate 4 artifact benchmark files from fresh run

  **What to do**:
  - Task 2 regenerated `artifacts/benchmarks/hand-*-latest.md` and `hand-*-latest.json` files
  - Verify all 8 files exist (4 .md + 4 .json) with current timestamps
  - Verify `hand-compact-latest.md` shows correct commit SHA and pass count
  - Stage all 8 artifact files for commit

  **Must NOT do**:
  - Do NOT manually edit artifact files — they are script-generated
  - Do NOT commit old artifact files

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Verification-only — artifacts already generated by Task 2

  **Parallelization**:
  - **Can Run In Parallel**: YES (with Tasks 4-9, 11)
  - **Parallel Group**: Wave 2
  - **Blocks**: None
  - **Blocked By**: Task 2 (benchmark run completed)

  **Acceptance Criteria**:
  - [ ] `ls artifacts/benchmarks/hand-compact-latest.md` → exists, timestamp = Task 2 run time
  - [ ] `ls artifacts/benchmarks/hand-semantic-latest.md` → exists
  - [ ] `ls artifacts/benchmarks/hand-json-latest.md` → exists
  - [ ] `ls artifacts/benchmarks/hand-plaintext-latest.md` → exists
  - [ ] Corresponding `.json` files all exist

  **QA Scenarios**:

  ```
  Scenario: All 8 artifact files regenerated
    Tool: Bash
    Steps:
      1. ls -lt artifacts/benchmarks/hand-*-latest.* | head -8
    Expected Result: 8 files (4 .md + 4 .json), all with matching recent timestamps
    Failure Indicators: Missing files or old timestamps
    Evidence: .omo/evidence/task-10-artifacts.txt
  ```

  **Commit**: YES (single commit with Tasks 7-9)
  - Message: `bench: regenerate all variant reports and artifacts from TRX`
  - Files: `docs/benchmarks/hand-semantic.md`, `docs/benchmarks/hand-json.md`, `docs/benchmarks/plaintext.md`, `artifacts/benchmarks/hand-*-latest.*`

---

- [x] 11. Translate tests/HybridTherapist.Integration/Cassettes/README.md to English

  **What to do**:
  - Translate title: "Kasety Ollama — nagrane interakcje pipeline'u" → "Ollama Cassettes — Recorded Pipeline Interactions"
  - Translate all Polish prose to natural English while preserving:
    - All JSON code blocks (unchanged)
    - Technical terms: "cassette", "pipeline", "wire-format", "CassetteOllamaServer"
    - All file paths and code references
    - The scenario table (translate descriptions, keep filenames)
  - Keep the AFDS frontmatter intact (already in English)
  - Ensure the "Why JSON-cassettes" section rationale is preserved faithfully

  **Must NOT do**:
  - Do NOT translate Polish test data content (user inputs in table)
  - Do NOT change JSON structure or any code examples
  - Do NOT add or remove sections

  **Recommended Agent Profile**:
  - **Category**: `writing`
    - Reason: Translation task requiring domain knowledge preservation
  - **Skills**: [`dotnet`]
    - `dotnet`: Understanding of .NET/cassette terminology to preserve technical accuracy

  **Parallelization**:
  - **Can Run In Parallel**: YES (with Tasks 4-10)
  - **Parallel Group**: Wave 2
  - **Blocks**: None
  - **Blocked By**: None

  **Acceptance Criteria**:
  - [ ] `grep -cP "[ąćęłńóśźż]" tests/HybridTherapist.Integration/Cassettes/README.md` → 0 (no Polish diacritics in prose)
  - [ ] Title is in English
  - [ ] All section headers are in English
  - [ ] Scenario table descriptions are in English
  - [ ] JSON code blocks are unchanged
  - [ ] File paths and code references are unchanged

  **QA Scenarios**:

  ```
  Scenario: No Polish diacritics in body text (test data exempted)
    Tool: Bash (grep)
    Steps:
      1. grep -nP "[ąćęłńóśźżĄĆĘŁŃÓŚŹŻ]" tests/HybridTherapist.Integration/Cassettes/README.md
    Expected Result: Only matches in test data strings (Polish user inputs like "chroniczne zamartwianie się") — zero in prose/headers
    Failure Indicators: Polish diacritics in section headers or explanatory text
    Evidence: .omo/evidence/task-11-diacritics.txt

  Scenario: Key technical sections preserved
    Tool: Bash (grep)
    Steps:
      1. grep -c "## Format" tests/HybridTherapist.Integration/Cassettes/README.md
      2. grep -c "## Why JSON-cassettes" tests/HybridTherapist.Integration/Cassettes/README.md
      3. grep -c "## Currently recorded" tests/HybridTherapist.Integration/Cassettes/README.md
    Expected Result: All 3 section headers exist (≥1 each)
    Failure Indicators: Missing section — content lost in translation
    Evidence: .omo/evidence/task-11-sections.txt
  ```

  **Commit**: YES
  - Message: `docs: translate cassettes README to English`
  - Files: `tests/HybridTherapist.Integration/Cassettes/README.md`

---

### Wave 3 — Polish + registry (MAX PARALLEL after Wave 2)

- [x] 12. Bump `last_verified` dates in 8 docs to 2026-06-06

  **What to do**:
  - Update `last_verified: 2026-05-23` → `last_verified: 2026-06-06` in these 8 files:
    - `docs/architecture.md` (line 14)
    - `docs/socrates-pipeline.md` (line 14)
    - `docs/security.md` (line 14)
    - `docs/api.md` (line 14)
    - `docs/layer-necessity.md` (line 14)
    - `docs/meta/doc-registry.md` (line 14)
    - `docs/meta/health-report.md` (line 14)
    - `tests/HybridTherapist.Integration/Cassettes/README.md` (line 13)
  - Do NOT touch: `docs/benchmarks/hand-compact.md` (already 2026-06-04), `docs/meta/glossary.md` (already 2026-06-01)

  **Must NOT do**:
  - Do NOT change any other frontmatter fields
  - Do NOT touch files where `last_verified` is already ≥ 2026-06-01 (glossary, hand-compact)

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: 8 identical string replacements across known files

  **Parallelization**:
  - **Can Run In Parallel**: YES (with Tasks 13-15)
  - **Parallel Group**: Wave 3
  - **Blocks**: None
  - **Blocked By**: None

  **Acceptance Criteria**:
  - [ ] `grep -c "last_verified: 2026-06-06" docs/architecture.md docs/socrates-pipeline.md docs/security.md docs/api.md docs/layer-necessity.md docs/meta/doc-registry.md docs/meta/health-report.md tests/HybridTherapist.Integration/Cassettes/README.md` → all return 1

  **QA Scenarios**:

  ```
  Scenario: All 8 files updated, none missed
    Tool: Bash (grep)
    Steps:
      1. for f in docs/architecture.md docs/socrates-pipeline.md docs/security.md docs/api.md docs/layer-necessity.md docs/meta/doc-registry.md docs/meta/health-report.md tests/HybridTherapist.Integration/Cassettes/README.md; do grep -l "last_verified: 2026-06-06" "$f"; done
    Expected Result: All 8 files listed
    Failure Indicators: Any file missing from output
    Evidence: .omo/evidence/task-12-dates.txt
  ```

  **Commit**: YES
  - Message: `docs: bump last_verified dates to 2026-06-06 (post-audit)`
  - Files: 8 files listed above

---

- [x] 13. Add explanatory comments for false-positive audit items (2 docs)

  **What to do**:
  - In `docs/architecture.md` line ~213 (Quality gates section), after the `[ANALYST CONTEXT]` mention, add a brief parenthetical: `(this is an intentional QA leakage pattern — QualityValidator.cs:138 explicitly checks for it)`
  - In `docs/socrates-pipeline.md` line ~93 (What this architecture buys), after the `S=crisis` mention, add: `(the S=crisis signal is a wire-format crisis flag used in resilience testing — see HandResponseDecoderTests.cs)`

  **Must NOT do**:
  - Do NOT change the actual `[ANALYST CONTEXT]` or `S=crisis` references — they stay
  - Do NOT add more than one sentence per doc

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Two short inline additions to existing paragraphs

  **Parallelization**:
  - **Can Run In Parallel**: YES (with Tasks 12, 14-15)
  - **Parallel Group**: Wave 3
  - **Blocks**: None
  - **Blocked By**: None

  **Acceptance Criteria**:
  - [ ] `grep "QA leakage pattern" docs/architecture.md` → 1 match
  - [ ] `grep "resilience testing" docs/socrates-pipeline.md` → 1 match

  **QA Scenarios**:

  ```
  Scenario: Comments present and correct
    Tool: Bash (grep)
    Steps:
      1. grep -n "QA leakage pattern.*QualityValidator" docs/architecture.md
      2. grep -n "resilience testing.*HandResponseDecoderTests" docs/socrates-pipeline.md
    Expected Result: Both return matches with line numbers
    Failure Indicators: Either grep returns empty
    Evidence: .omo/evidence/task-13-comments.txt
  ```

  **Commit**: YES (groups with Task 12)
  - Message: `docs: add explanatory comments for ANALYST CONTEXT and S=crisis patterns`
  - Files: `docs/architecture.md`, `docs/socrates-pipeline.md`

---

- [x] 14. Fill doc-registry.md missing entries (AGENTS.md, CHANGELOG.md, README.md, cassettes/README.md)

  **What to do**:
  - Add 4 new rows to the RULES table in `docs/meta/doc-registry.md`:
    - `ref.agents` | `AGENTS.md` | ref | active | Build/test instructions and key invariants for AI agents
    - `ref.changelog` | `CHANGELOG.md` | ref | active | Release history and breaking changes
    - `ref.readme` | `README.md` | ref | active | Project overview, quick start, and architecture summary
    - `ref.cassettes` | `tests/HybridTherapist.Integration/Cassettes/README.md` | ref | active | VCR-style cassettes for offline pipeline testing

  **Must NOT do**:
  - Do NOT change existing table structure or column order
  - Do NOT remove any existing entries

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: 4 row additions to a simple markdown table

  **Parallelization**:
  - **Can Run In Parallel**: YES (with Tasks 12-13, 15)
  - **Parallel Group**: Wave 3
  - **Blocks**: None
  - **Blocked By**: None

  **Acceptance Criteria**:
  - [ ] `grep -c "ref.agents" docs/meta/doc-registry.md` → 1
  - [ ] `grep -c "ref.changelog" docs/meta/doc-registry.md` → 1
  - [ ] `grep -c "ref.readme" docs/meta/doc-registry.md` → 1
  - [ ] `grep -c "ref.cassettes" docs/meta/doc-registry.md` → 1

  **QA Scenarios**:

  ```
  Scenario: All 4 entries present
    Tool: Bash (grep)
    Steps:
      1. grep "ref.agents\|AGENTS.md\|Build/test instructions" docs/meta/doc-registry.md
      2. grep "CHANGELOG.md\|Release history" docs/meta/doc-registry.md
      3. grep "ref.readme\|README.md\|Project overview" docs/meta/doc-registry.md
      4. grep "ref.cassettes\|Cassettes/README.md\|VCR-style" docs/meta/doc-registry.md
    Expected Result: All 4 greps return matches
    Failure Indicators: Any missing
    Evidence: .omo/evidence/task-14-registry.txt
  ```

  **Commit**: YES (groups with Task 12-13)
  - Message: `docs: add AGENTS.md, CHANGELOG.md, README.md, cassettes/README.md to doc-registry`
  - Files: `docs/meta/doc-registry.md`

---

- [x] 15. Regenerate health-report.md via CI (or manual proxy)

  **What to do**:
  - Option A (preferred): Trigger the `docs-validation.yml` CI workflow to regenerate `docs/meta/health-report.md`
  - Option B (fallback): If CI is unavailable, manually update:
    - "Last scan" date to current
    - "Documents tracked" to 14 (match doc-registry count)
    - "Documents passing validation" to 14 (assuming all pass)
    - "Documents needing review" to 0

  **Must NOT do**:
  - Do NOT change the PURPOSE, SCOPE, or structure sections
  - Do NOT edit CI-generated metrics unless CI is unavailable

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Either CI trigger or 3-line manual update in a known file

  **Parallelization**:
  - **Can Run In Parallel**: YES (with Tasks 12-14)
  - **Parallel Group**: Wave 3
  - **Blocks**: None
  - **Blocked By**: None

  **Acceptance Criteria**:
  - [ ] "Last scan" date is 2026-06-06 (or current)
  - [ ] "Documents tracked" ≥ 14
  - [ ] "Documents passing validation" ≥ 14
  - [ ] "Documents needing review" = 0

  **QA Scenarios**:

  ```
  Scenario: Health report updated
    Tool: Bash (grep)
    Steps:
      1. grep "Last scan" docs/meta/health-report.md
      2. grep "Documents tracked" docs/meta/health-report.md
    Expected Result: Date is current, count is ≥14
    Failure Indicators: Old date or count still 8
    Evidence: .omo/evidence/task-15-health.txt
  ```

  **Commit**: YES
  - Message: `docs: regenerate health-report.md (post-audit, 14 docs tracked)`
  - Files: `docs/meta/health-report.md`

---

## Final Verification Wave (MANDATORY — after ALL implementation tasks)

> 4 review agents run in PARALLEL. ALL must APPROVE.

- [x] F1. **Plan Compliance Audit** — `oracle` *(running bg)*
  Read the plan end-to-end. For each "Must Have": verify implementation exists (grep the target file). For each "Must NOT Have": search for forbidden patterns — reject with file:line if found. Check evidence files exist in .omo/evidence/.
  Output: `Must Have [N/N] | Must NOT Have [N/N] | Tasks [15/15] | VERDICT: APPROVE/REJECT`

- [x] F2. **Build + Grep Verification** — `quick` *(running bg)*
  Run `dotnet build HybridTherapist.sln -c Release --nologo -v q`. Run all grep assertions from acceptance criteria. Verify all QA evidence files exist.
  Output: `Build [PASS/FAIL] | grep-purity [PASS/FAIL] | grep-dates [PASS/FAIL] | VERDICT`

- [x] F3. **Manual QA — Read All Changed Files** — `quick` *(running bg)*
  Read every changed file in full. Verify no broken formatting, no unintended content changes, all cross-references valid, AFDS frontmatter intact, benchmark numbers internally consistent.
  Output: `Files reviewed [N/N] | Formatting [OK/N] | Consistency [OK/N] | VERDICT`

- [x] F4. **Scope Fidelity Check** — `deep` *(running bg)*
  Verify 1:1 — everything in spec was built, nothing beyond spec was built. Check "Must NOT do" compliance. Detect cross-task contamination.
  Output: `Tasks [15/15 compliant] | Contamination [CLEAN/N] | Unaccounted [CLEAN/N] | VERDICT`

---

## Commit Strategy

All changes grouped into 5 atomic commits:

| # | Commit Message | Files |
|---|---------------|-------|
| **1** | `docs: fix resilience ladder 5→6 across all docs (HandCodec v0.3.0)` | `docs/socrates-pipeline.md`, `docs/architecture.md`, `docs/meta/glossary.md` |
| **2** | `bench: regenerate all variant reports and artifacts from fresh TRX run` | `docs/benchmarks/hand-semantic.md`, `docs/benchmarks/hand-json.md`, `docs/benchmarks/plaintext.md`, `artifacts/benchmarks/hand-*-latest.*` |
| **3** | `docs: translate cassettes README to English` | `tests/HybridTherapist.Integration/Cassettes/README.md` |
| **4** | `docs: bump last_verified dates + explanatory comments + registry entries` | `docs/architecture.md`, `docs/socrates-pipeline.md`, `docs/security.md`, `docs/api.md`, `docs/layer-necessity.md`, `docs/meta/doc-registry.md`, `docs/meta/health-report.md` |
| **5** | `docs: regenerate health-report.md (post-audit)` | `docs/meta/health-report.md` |

Pre-commit: `dotnet build HybridTherapist.sln -c Release --nologo -v q`

---

## Success Criteria

### Verification Commands
```bash
# Resilience ladder: no 5-level references remain
grep -r "5.level.*resilience\|5-level.*degradation" docs/*.md docs/**/*.md
# Expected: zero output

# Resilience ladder: 6-level present in all 3 critical docs
grep -c "6.level\|Level 6.*passthrough\|6-level degradation" docs/socrates-pipeline.md docs/architecture.md docs/meta/glossary.md
# Expected: ≥1 per file

# Cassettes README: no Polish in prose
grep -nP "[ąćęłńóśźżĄĆĘŁŃÓŚŹŻ]" tests/HybridTherapist.Integration/Cassettes/README.md
# Expected: matches only in quoted Polish test strings

# Dates: all 8 files bumped
for f in docs/architecture.md docs/socrates-pipeline.md docs/security.md docs/api.md \
         docs/layer-necessity.md docs/meta/doc-registry.md docs/meta/health-report.md \
         tests/HybridTherapist.Integration/Cassettes/README.md; do
  grep -l "last_verified: 2026-06-06" "$f"
done
# Expected: all 8 files listed

# Build: zero warnings
dotnet build HybridTherapist.sln -c Release --nologo -v q 2>&1 | grep -c "warning"
# Expected: 0
```

### Final Checklist
- [ ] All "Must Have" present: resilience 6-level, correct benchmarks, English README, updated dates
- [ ] All "Must NOT Have" absent: no CHANGELOG changes, no code changes, no ANALYST CONTEXT removal
- [ ] Build passes with 0 warnings
- [x] 5 atomic commits ready
- [ ] All evidence files in .omo/evidence/
