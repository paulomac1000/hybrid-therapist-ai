# Fix — Therapeutic quality check: soft-fail to L4 before hard-block

## TL;DR

> **Root Cause**: `ValidateTherapeuticQuality` wykrywa formułkowe otwarcie z L6 → twardo blokuje odpowiedź (`BuildFallback`). Tymczasem EN-side QA (`ValidateEnglishDraft`) robi soft-fail do L4 draft. Architektura jest niespójna.
>
> **Fix**: Therapeutic check → soft-fail do L4 draft. Hard-block tylko gdy L4 też nie przejdzie checku.

---

## Diagnosis

### Log
```
warn: Therapeutic quality check failed for user_xxx: formulaic_opening — blocking response
```

### Kod (TherapistFlow.cs:216-221)
```csharp
QualityValidator.Verdict tq = QualityValidator.ValidateTherapeuticQuality(enResponse, ...);
if (!tq.Ok)
{
    // ❌ Hard block — użytkownik dostaje "Przepraszam, mam chwilowe trudności techniczne"
    return BuildFallback(request.Model, sessionId, "L6_therapeutic_quality", tq.Reason);
}
```

Dla porównania, EN-side QA (linia 209):
```csharp
QualityValidator.Verdict qa1 = QualityValidator.ValidateEnglishDraft(enResponse, userTextEn);
if (!qa1.Ok)
{
    // ✅ Soft-fail — wraca do L4 draft
    enResponse = l4.Text;
}
```

### Dlaczego to boli
L6 Calibrator (Llama4-Dolphin 8B) po zahartowanym prompcie wprowadza formułkowe otwarcia (np. "I'm here to help"). QualityValidator je słusznie wykrywa, ale zamiast wrócić do nieskażonego L4 draft, blokuje całą odpowiedź.

---

## TODOs

- [ ] 1. Soft-fail therapeutic quality check — try L4 draft before hard-block

  **File**: `src/HybridTherapist.Application/Flows/TherapistFlow.cs`

  **What**: Po niepowodzeniu `ValidateTherapeuticQuality(enResponse, ...)`, najpierw spróbuj `l4.Text` (draft przed L6), sprawdź go ponownie, i dopiero wtedy hard-block jeśli oba failują.

  **Agent**: `quick`

- [ ] 2. Rebuild container + test

  **Test**: Curl "witaj" / "nie mogę zasnąć" → Fallback: False, response ≠ "Przepraszam..."

  **Agent**: `quick`

---

## Commit Strategy

| Commit | Files |
|--------|-------|
| `fix: soft-fail therapeutic quality check to L4 before hard-block` | TherapistFlow.cs |
