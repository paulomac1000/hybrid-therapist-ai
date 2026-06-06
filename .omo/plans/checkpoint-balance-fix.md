# Fix — L2 checkpoint over-fitting (detailed analysis on minimal input)

## TL;DR

> **Root Cause**: Wszystkie 3 L2 Analyst checkpointy mają szczegółowe `y1=cognitive_patterns`, `x4=risk_indicators`, `q3=evidence_quotes`. Model nie widzi ani jednego przykładu gdzie `x4=none` lub `y1=none`. W efekcie fabrykuje zmyślone detale dla "I can't sleep" → `racing_thoughts`, `worrying_thoughts`, `can't stop thinking about negative things`.
>
> **Fix**: Zróżnicować checkpointy: jeden szczegółowy, jeden konserwatywny (none), jeden umiarkowany.

---

## Diagnosis (z trace)

### Input → Output
```
"nie mogę zasnąć" → "I can't sleep"
Analyst: y1=racing_thoughts | x4=worrying_thoughts | q3="can't stop thinking about negative things"
```

User NIE powiedział nic o myślach. Analista zmyślił "racing thoughts" i "negative thinking".

### Dlaczego
Wszystkie 3 checkpointy uczą model że trzeba wypełniać `y1`, `x4`, `q3` szczegółowymi wartościami nawet gdy brak danych. Model nie widział nigdy przykładu gdzie `x4=none` lub `y1=none`.

### Obecne checkpointy
```
e7=exhaustion|s9=moderate|x4=insomnia_worry|y1=catastrophizing|q3="haven't slept..."
e7=anxiety|s9=high|x4=panic_fear|y1=racing_thoughts|q3="constantly worried..."
e7=sadness|s9=moderate|x4=hopelessness|y1=rumination|q3="nothing matters..."
```
Wszystkie SZCZEGÓŁOWE → model overfituje.

---

## TODOs

- [ ] 1. Diversify L2 checkpoints — 1 detailed, 1 conservative, 1 moderate

  **File**: `src/HybridTherapist.Application/Hand/HandCheckpointLibrary.cs`

  **What**:
  ```
  // Szczegółowy (gdy user faktycznie podał kontekst):
  e7=anxiety|s9=high|x4=panic_fear|y1=racing_thoughts|q3="constantly worried about everything"
  
  // Konserwatywny (gdy user powiedział mało, NIE fabrykujemy):
  e7=fatigue|s9=moderate|x4=none|y1=unspecified|q3="can't sleep at night"
  
  // Umiarkowany (trochę kontekstu):
  e7=sadness|s9=moderate|x4=social_isolation|y1=rumination|q3="nothing matters anymore"
  ```

  Kluczowe: `x4=none` w konserwatywnym przykładzie uczy model że można powiedzieć "brak wskaźników ryzyka" gdy użytkownik nic nie ujawnił.

  **Agent**: `quick`

- [ ] 2. Rebuild container + test with curl

  **Test**: "nie mogę zasnąć" → response NIE powinna wspominać "wir myśli", "natłok", "racing thoughts"
  
  **Agent**: `quick`

---

## Commit

| Commit | Files |
|--------|-------|
| `fix: diversify L2 checkpoints to prevent over-fitting on minimal input` | HandCheckpointLibrary.cs |
