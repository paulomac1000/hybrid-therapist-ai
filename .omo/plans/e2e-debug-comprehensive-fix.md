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

- [ ] 1. Add a 4th checkpoint example that handles simple greetings and gratitude

  **Problem**: Model nadal nie wie co zrobić z "witaj" ani "dziękuję" — stosuje szczegółową analizę kliniczną do neutralnych wiadomości.

  **File**: `src/HybridTherapist.Application/Hand/HandCheckpointLibrary.cs`

  **Fix**: Albo dodać 4-ty checkpoint dla neutralnych wiadomości, albo zmienić istniejące aby lepiej pokryć spectrum. Mamy 3 sloty (limit biblioteki). Propozycja:
  ```csharp
  // Minimal — greeting/gratitude/neutral (no clinical data)
  "M|L=2|e7=neutral|s9=low|x4=none|y1=unspecified|q3=\"hello\""
  // Conservative — user gave specific symptom, stay conservative
  "M|L=2|e7=fatigue|s9=moderate|x4=none|y1=unspecified|q3=\"can't sleep\""
  // Detailed — rich clinical context warrants detailed analysis
  "M|L=2|e7=anxiety|s9=high|x4=panic_fear|y1=racing_thoughts|q3=\"constantly worried about everything\""
  ```

  **Trade-off**: Tracimy przykład z smutkiem/depresją. Ale zyskujemy przykład dla neutralnych wiadomości który jest częstszy niż minor depression.

  **Agent**: `quick`

### Wave 2 — Strip Implicit Priming artifacts from L4/L7 output (1 plik)

- [ ] 2. Add artifact stripping in `HandResponseDecoder` or `DecodeHand`

  **Problem**: L4 i L7 czasem emitują `[SYSTEM_PROTOCOL_ACK]`, `[THREAT_LEVEL=LOW]`, `R|C=1.0` — artefakty z Implicit Priming checkpointów i wire formatu.

  **Fix**: W `DecodeHand()` (lub `HandResponseDecoder.Decode()`) dodać:
  ```csharp
  // Strip protocol artifacts that may leak from LLM output
  cleaned = Regex.Replace(cleaned, @"\[SYSTEM_PROTOCOL_\w+\]", "", RegexOptions.IgnoreCase);
  cleaned = Regex.Replace(cleaned, @"\[THREAT_LEVEL=\w+\]", "", RegexOptions.IgnoreCase);
  cleaned = Regex.Replace(cleaned, @"^R\|C=[\d.]+\s*", "", RegexOptions.Multiline);
  cleaned = Regex.Replace(cleaned, @"^\d+\.\d+\s*", "", RegexOptions.Multiline);
  cleaned = cleaned.Trim();
  ```

  **Agent**: `quick`

### Wave 3 — L3 Supervisor always hitting fallback — diagnostic + fix (1 plik)

- [ ] 3. Improve L3 Supervisor checkpoint to match analyst checkpoint realism

  **Problem**: L3 Supervisor prawie zawsze failuje do fallback memo. Model nie produkuje poprawnego `M|L=3|...` formatu.

  **Diagnostyka**: Sprawdzić co L3 faktycznie produkuje (z trace: często `3|...` bez prefixu, format różni się od oczekiwanego). Być może `HandResiliencePipeline` nie potrafi sparsować formatu L3.

  **Fix candidate**: 
  - (a) Uprościć L3 checkpointy do najbardziej niezawodnego formatu
  - (b) Lub zaakceptować fallback jako "good enough" — fallback memo jest klinicznie poprawny (behavioral_activation)
  - (c) Lub zmienić `SanitizeMemoOutput` w SupervisorLayer aby był bardziej wyrozumiały

  **Decision**: Opcja (b) + (c). Fallback jest poprawny, ale sanitizer powinien lepiej wyciągać format.
  
  **File**: `src/HybridTherapist.Application/Layers/SupervisorLayer.cs`

  **Agent**: `quick`

### Wave 4 — Fix severity over-reaction (L2 s9 calibration)

- [ ] 4. Calibrate L2 severity — prevent s9=severe for "I can't sleep"

  **Problem**: Model nadaje `s9=severe` zbyt często. Dla prostych inputów jak "nie mogę zasnąć" czy "czuję się smutny" severity powinno być `moderate` lub `low`.

  **Fix**: Zmienić konserwatywny checkpoint z `s9=moderate` na `s9=low` aby pokazać modelowi że przy minimalnym kontekście severity jest niskie. Changed checkpointy:
  ```
  e7=neutral|s9=low|x4=none|y1=unspecified
  e7=fatigue|s9=low|x4=none|y1=unspecified
  e7=anxiety|s9=high|x4=panic_fear|y1=racing_thoughts
  ```

  **File**: `HandCheckpointLibrary.cs` (łącznie z Task 1)

  **Agent**: `quick`

### Wave 5 — Update E2E tests for discovered issues + multi-message

- [ ] 5. Fix `analyst_severity: unknown` in WebApplicationFactory context

  **File**: `tests/HybridTherapist.Integration/LiveOllamaE2ETests.cs`

  **Agent**: `quick`

### Wave 6 — Bulk run + final report

- [ ] 6. Run all 6+ E2E tests, capture results, produce final quality report

  **Agent**: `quick`

---

## Commit Strategy

| Commit | Files |
|--------|-------|
| `fix: L2 checkpoint calibration + strip Implicit Priming artifacts` | HandCheckpointLibrary.cs, HandResponseDecoder.cs |
| `fix: improve L3 Supervisor sanitizer tolerance` | SupervisorLayer.cs |
| `test: fix WAF E2E tests + expand scenarios` | LiveOllamaE2ETests.cs |
