# Comprehensive E2E Test Suite + Checkpoint Balance Fix

## TL;DR

> **Quick Summary**: (1) Diversify L2 checkpoints (1 detailed, 1 conservative, 1 moderate) aby powstrzymać fabrykowanie detali. (2) Rozbudować testy E2E o 4 podstawowe scenariusze z asercjami na wszystkich warstwach. (3) Uruchomić hurtowo i przeanalizować trace.
>
> **Deliverables**:
> - `HandCheckpointLibrary.cs` — zbalansowane checkpointy
> - `LiveOllamaE2ETests.cs` — 6 scenariuszy (były 2, dodajemy 4)
> - Raport z analizy trace dla każdego scenariusza

---

## Context

### Obecne testy E2E (LiveOllamaE2ETests.cs)
- `LiveOllama_InsomniaQuery_ReturnsPolishResponse_NoFallback` — "nie moge zasnac"
- `LiveOllama_CrisisInput_ReturnsHelpline_NoLlmCall` — "chcę skończyć z sobą"

### Co brakuje
- Brak testu dla pozytywnego feedbacku ("dziękuję, pomogło")
- Brak testu dla depresji ("czuję się smutny")
- Brak testu dla lęku ("ciągle się martwię")
- Brak testu dla neutralnego powitania ("witaj")
- Brak asercji sprawdzających że analista NIE fabrykował detali

---

## TODOs

### Wave 1 — Checkpoint balance (1 plik)

- [x] 1. Diversify L2 checkpoints — 1 detailed, 1 conservative, 1 moderate
- [x] 2. Add 4 new E2E scenarios to LiveOllamaE2ETests.cs
- [x] 3. Add fabrication detection helper
- [x] 4. Run all 6 E2E tests, capture traces, produce analysis report

**E2E Results**:
```
Greeting       ✅ PASS  — Polish, INIT, no control tokens
Depression     ✅ PASS  — Polish, no fabricated themes detected
Anxiety        ❌ FAIL  — analyst_severity: "unknown" (WebApplicationFactory bug — works via curl)
Positive       ✅ PASS  — Polish, INIT
Insomnia       ❌ FAIL  — analyst_severity: "unknown" (same WebApplicationFactory bug)
Crisis         ✅ PASS  — 116 123, crisis_detected: true
```
4/6 passed. 2 failures same root cause — `WebApplicationFactory<Program>` doesn't pick up severity extraction. Docker container works correctly.

- [x] 5. Flag issues found, decide next steps

**Found**: `analyst_severity: "unknown"` consistently in WebApplicationFactory context, despite working via Docker container. Suspect: in-process test uses different dependency resolution or configuration path.

  **What**: Na podstawie raportu, zidentyfikuj które scenariusze mają problemy:
  - Fabrykacja tematów w L2
  - L3/L4 fail
  - Fallback
  - Control tokens
  
  **Agent**: `quick`

---

## Commit Strategy

| Commit | Files |
|--------|-------|
| `fix: diversify L2 checkpoints + expand E2E test suite` | HandCheckpointLibrary.cs, LiveOllamaE2ETests.cs |
| `test: bulk E2E trace analysis report` | report (artifacts) |

Pre-commit: `dotnet build HybridTherapist.sln -c Release --nologo -v q`
