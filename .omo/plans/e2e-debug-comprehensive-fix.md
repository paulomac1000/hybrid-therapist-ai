# Comprehensive E2E Debug + Fix Plan

## TL;DR

> **Quick Summary**: Bulk trace 5 scenariuszy przez kontener Docker (4/6 testów E2E przechodzi, WAF pokazuje 2 false-fails). Znalezione 3 kategorie problemów: (1) L2 nadal overfituje dla "witaj" i "dziękuję" — konserwatywny checkpoint nie wystarcza. (2) L3 Supervisor **zawsze failuje** do fallback memo. (3) L4 emituje artefakty Implicit Priming (`[SYSTEM_PROTOCOL_ACK]`, `[THREAT_LEVEL]`).

---

## Bulk Trace Results (via Docker, 5 scenarios)

| Scenario | L2 e7 | L2 s9 | L2 y1/x4 | L3 approach | L4/L7 quality | Issues |
|----------|-------|-------|----------|-------------|---------------|--------|
| "nie mogę zasnąć" | insomnia | severe | unspecified/none ✅ | fallback | OK | s9=severe over-react |
| "witaj" | **anger** ❌ | high | irritability/intolerance | fallback | L7 has `[SYSTEM_PROTOCOL_ACK]` ❌ | total misanalysis |
| "czuję się smutny" | depression | severe | loss of interest/fatigue | fallback | L4 has `[THREAT_LEVEL]` ❌ | s9=severe, threat marker |
| "martwię się o pracę" | fatigue | high | overwhelmed/work_stress | **problem_solving** ✅ | OK | best result |
| "dziękuję, pomogło" | **fatigue** ❌ | moderate | unspecified/none | fallback | L4 has `[SYSTEM_PROTOCOL_ACK]` ❌ | completely wrong |

**Kluczowe obserwacje**:
- L3 Supervisor **zawsze używa fallback memo** (4/5 scenariuszy). Jedyny exception: anxiety scenario.
- L4/L7 czasem emitują artefakty Implicit Priming (`[SYSTEM_PROTOCOL_ACK]`, `[THREAT_LEVEL]`)
- L2 konserwatywny checkpoint działa świetnie dla "nie mogę zasnąć" (x4=none, y1=unspecified) ale nie pomaga dla innych

---

## TODOs

### Wave 1 — Fix L2 checkpoint for remaining over-fitting cases (1 plik)

- [x] 1. Add a 4th checkpoint example that handles simple greetings and gratitude
- [x] 2. Add artifact stripping in `HandResponseDecoder` or `DecodeHand`
- [x] 3. Improve L3 Supervisor checkpoint to match analyst checkpoint realism
- [x] 4. Calibrate L2 severity — prevent s9=severe for "I can't sleep"
- [x] 5. Fix `analyst_severity: unknown` in WebApplicationFactory context
- [x] 6. Run all 6+ E2E tests, capture results, produce final quality report

  **Agent**: `quick`

---

## Commit Strategy

| Commit | Files |
|--------|-------|
| `fix: L2 checkpoint calibration + strip Implicit Priming artifacts` | HandCheckpointLibrary.cs, HandResponseDecoder.cs |
| `fix: improve L3 Supervisor sanitizer tolerance` | SupervisorLayer.cs |
| `test: fix WAF E2E tests + expand scenarios` | LiveOllamaE2ETests.cs |
