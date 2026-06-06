# CodeRabbit Findings Fix — 10 Issues

## TL;DR

> **Quick Summary**: Naprawa 10 znalezisk CodeRabbit CLI — od trywialnych (daty, deduplikacja, wersje) po średnie (JSON parse handling, PII leak).
>
> **Deliverables**:
> - CHANGELOG: HandCodec 0.3.0 → 0.4.0
> - 3 pliki src: TopicRegistry dedup, TherapistFlow severity, AnalystLayer JSON/PII
> - 2 walidatory testowe: L4ForbiddenInstructionMarkers usage + BENCHMARK_TOKEN_SAVINGS
> - 4 pliki testowe: test assertions fixes
> - AGENTS.md: last_verified bump

---

## TODOs

### Wave 1 — Trivial fixes (MAX PARALLEL, 5 files)

- [x] 1. CHANGELOG.md: update HandCodec/HandRuntime 0.3.0 → 0.4.0
  **File**: `CHANGELOG.md` lines 18-29 | **Agent**: `quick`
  Replace `0.3.0` with `0.4.0` in the HandCodec upgrade entry and local packages note.

- [x] 2. AGENTS.md: bump last_verified to 2026-06-06
- [x] 3. TopicRegistry.cs: add Distinct() to deduplicate topics
- [x] 4. TherapistFlow.cs: use actual analyst_severity in metadata
- [x] 5. HandLongSessionDriftBenchmarkTests.cs: add BENCHMARK_TOKEN_SAVINGS marker
- [x] 6. AnalystLayer.cs: fix JSON parse failure returns success
- [x] 7. AnalystLayer.cs: fix fallback memo PII leak
- [x] 8. HandJsonBenchmarkValidator.cs: use L4ForbiddenInstructionMarkers
- [x] 9. HandPlaintextBenchmarkValidator.cs: use L4ForbiddenInstructionMarkers
- [x] 10. ImplicitPrimingTests.cs: validate AssistantWire for therapeutic terms
  **File**: `tests/HybridTherapist.Tests/ImplicitPrimingTests.cs` lines 142-156 | **Agent**: `quick`
  Extend `AllCheckpointExchanges_AreNonDomain` to also assert that `ex.AssistantWire` does NOT contain clinical/therapeutic terms (anxiety, depression, therapeutic, clinical, patient, etc.).

---

## Commit Strategy

| Commit | Files |
|--------|-------|
| `fix: CodeRabbit — trivial fixes (CHANGELOG, AGENTS, TopicRegistry, TherapistFlow, Drift)` | 5 files |
| `fix: CodeRabbit — analyst layer JSON/PII + validator markers` | 3 files |
| `fix: CodeRabbit — test assertion improvements` | 2 files |

Pre-commit: `dotnet build HybridTherapist.sln -c Release --nologo -v q`
