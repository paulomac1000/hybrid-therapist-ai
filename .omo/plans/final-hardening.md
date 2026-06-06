# Final Hardening — E2E tests, new cassettes, L6 Calibrator latch

## TL;DR

> **Quick Summary**: Trzy ostatnie wzmocnienia przed mergem PR #11 — test E2E wyłapujący regresje na żywym pipeline, przegrane kasety z aktualnymi modelami, oraz brakujący latch na control tokeny w L6 Calibrator.

---

## Context

### Co przeszło (20 commitów)
- Dokumentacja: resilience 5→6, benchmarki, tłumaczenie, daty
- Kod: .NET 10, HandCodec v0.4.0, SonarQube 28→0, CodeRabbit 12 fixów
- Infra: Docker `build: .`, control token stripping (L1/L2/L3/L4/L7)

### Co brakuje
1. **E2E test** — nic nie sprawdza live pipeline pod kątem regresji (control tokens, fallbacki, format)
2. **Nowe kasety** — stare nie pokrywają `<|control_N|>` zachowania nowych modeli
3. **L6 Calibrator** — `resp.Text.Trim()` bez strippingu control tokenów

---

## TODOs

### Wave 1 — L6 Calibrator latch (1 plik)

- [x] 1. Add control token stripping in L6 Calibrator path
- [x] 2. Add E2E smoke test against live Docker Compose pipeline
- [x] 3. Add E2E crisis hard-stop test
- [x] 6. Verify existing cassettes still pass

  **What**: `dotnet test tests/HybridTherapist.Integration -c Release --nologo` → wszystkie 63/63 przechodzą

  **Agent**: `quick`

---

## Commit Strategy

| Commit | Files |
|--------|-------|
| `fix: add control token stripping to L6 Calibrator path` | TherapistLayerService.cs |
| `test: add E2E smoke tests against live Docker pipeline` | E2ESmokeTests.cs |
| `test: record new cassettes with current model behavior` | e2e-smoke-witaj.json, e2e-control-strip.json |

Pre-commit: `dotnet build HybridTherapist.sln -c Release --nologo -v q`
