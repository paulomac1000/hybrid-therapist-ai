# Fix — L2 Analyst checkpoint realism + L6 Calibrator discipline

## TL;DR

> **Quick Summary**: Dwie naprawy architektoniczne: (1) wzbogacenie checkpointów L2 Analyst o klinicznie realistyczne odpowiedzi (model uczy się analizy klinicznej, nie tylko formatu), (2) zaostrzenie prompta L6 Calibrator (zakaz dodawania formułkowych zapewnień i dodatkowych pytań).
>
> **Impact**: L2 przestanie halucynować "excited/happy" dla "nie mogę zasnąć". L6 przestanie degradować jakość odpowiedzi.

---

## Diagnosis (z trace)

### L2 Analyst — halucynacja spowodowana trywialnymi checkpointami

**Co widzieliśmy:**
```
"I can't sleep" → M|L=2|e7=excited|s9=high|x4=None|y1=happy|q3="great!"
```
MentaLLaMA myli realny input z testowym pingiem. Output modelu: *"These are the results of a series of pings..."*

**Root cause:** Checkpointy uczą TYLKO formatu M\|, nie analizy klinicznej:
```
[SYSTEM_PROTOCOL_PING] → M|L=2|e7=neutral|s9=low|...   ← za słabe, za neutralne
[SYSTEM_PROTOCOL_PING] → M|L=2|e7=content|s9=low|...     ← bezobjawowe
[SYSTEM_PROTOCOL_PING] → M|L=2|e7=worried|s9=low|...     ← zbagatelizowane
```
Trzy przykłady z `s9=low` uczą model że WSZYSTKO jest niskiego ryzyka. Zero przykładów z realistycznymi stanami klinicznymi (`exhaustion`, `anxiety_moderate`, `hopelessness`).

**Invariant do zachowania:** `[SYSTEM_PROTOCOL_PING]` musi być zero-terapeutyczny (test `SystemPing_ContainsNoTherapeuticWords`). TYLKO user text jest sprawdzany — assistant response MOŻE zawierać kliniczne terminy.

### L6 Calibrator — dodaje formułki zamiast polerować

**Co widzieliśmy:**
```
L4: "I sense a deep frustration..." (1 pytanie otwarte, dobra odpowiedź)
L6: "Could you tell me more... What thoughts... I'm here to help..." (2 pytania + formułka)
```
Kalibrator miał polerować styl, a dodał: dodatkowe pytanie, formułkowe zapewnienie, wydłużył odpowiedź.

**Root cause:** Prompt L6 mówi "End with one open-ended question" ale nie zabrania DODAWANIA pytań. I nie blokuje formułkowych zakończeń.

---

## TODOs

### Wave 1 — L2 checkpoint enrichment (1 plik)

- [x] 1. Replace TherapyAnalystPing with clinically realistic responses
- [x] 2. Harden L6 Calibrator prompt against formulaic additions
- [x] 3. Rebuild container + test via curl

  **Test**: `curl "nie mogę zasnąć"` → odpowiedź:
  - ma jedno pytanie (nie dwa)
  - nie zawiera "Jestem tutaj, aby Cię wesprzeć" ani podobnych formułek
  - kończy się znakiem zapytania (nie formułką)

  **Agent**: `quick`

---

## Commit Strategy

| Commit | Files |
|--------|-------|
| `fix: enrich L2 Analyst checkpoints with clinical realism` | HandCheckpointLibrary.cs |
| `fix: harden L6 Calibrator prompt against formulaic additions` | TherapistLayerService.cs |

---

## Expected impact

| Metryka | Przed | Po |
|---------|-------|-----|
| L2 halucynacja "excited" dla insomnia | ✅ występuje | ❌ powinno być fatigue/anxiety |
| L6 dodaje formułki | ✅ występuje | ❌ tylko poleruje styl |
| Liczba pytań w odpowiedzi | 2-3 | 1 |
| Końcowe zapewnienia | "Jestem tutaj aby Cię wesprzeć" | brak |
