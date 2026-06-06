# Fix — PsychoCounsel `<|control_N|>` token leakage

## TL;DR

> **Root Cause**: PsychoCounsel 8B (Llama-3 based) emituje `<|control_8|>...internal thinking...<|control_9|>` przed każdą odpowiedzią. `HandResponseDecoder` nie usuwa tych tokenów — surowe myślenie modelu + tokeny przeciekają do użytkownika.
>
> **Impact**: Użytkownik widzi wewnętrzny monolog modelu po angielsku przed polską odpowiedzią + kontrolne tokeny formatujące. Drabina rezyliencji wpada na Level 6 (passthrough) zamiast Level 1-2, bo tokeny psują format `R|C=...`.
>
> **Fix**: Dodać stripping `<|control_N|>` bloków w `HandResponseDecoder.Decode()` — przed przekazaniem do runtime resilience pipeline.

---

## Diagnosis

### Objawy
1. Odpowiedź zawiera `<|control_8|>Got it, the user is asking...<|control_9|>` przed właściwą treścią
2. Tokeny `<|control_N|>` nie są usuwane przez `HandRuntime.HandResponseDecoder`
3. Drabina rezyliencji L4/L6 spada do Level 6 (passthrough) zamiast Level 1-2
4. Myślenie modelu (po angielsku) jest widoczne dla użytkownika końcowego

### Logs potwierdzające
```
Hand decode: resilience level 6, confidence 0.5   ← powinno być Level 1-2
```

### Kod
`DecodeHand()` → `HandResponseDecoder.Decode()` → `HandRuntime.HandResponseDecoder.Decode()` — tokeny kontrolne nie są usuwane na żadnym etapie.

---

## TODOs

- [x] 1. Add control token stripping in `HandResponseDecoder.Decode()`

  **File**: `src/HybridTherapist.Application/Hand/HandResponseDecoder.cs`
  **Also added**: `AnalystLayer.SanitizeMemoOutput` + `SupervisorLayer.SanitizeMemoOutput` (L2/L3 path)

- [x] 2. Verify — no control tokens in user-facing output

  **Test**: curl "nie mogę zasnąć" → 0 `<|control_` tokenów, Fallback: False ✅
  **Test**: curl "witaj" → 0 `<|control_` tokenów, Fallback: False ✅

- [x] 3. Rebuild container + test

  **Skrypt**: `./scripts/rebuild-therapist.sh` — ok

---

## Commit Strategy

| Commit | Files |
|--------|-------|
| `fix: strip PsychoCounsel control tokens before resilience decode` | `HandResponseDecoder.cs` |

---

## Why tests didn't catch this

Kasetowe testy integracyjne używają nagranych odpowiedzi Ollama. Kasety zostały nagrane z modeli, które nie emitowały `<|control_N|>` tokenów (wczesna wersja) LUB zostały nagrane gdy tokeny były już w kasecie i testy je akceptują.

Nowe modele (PsychoCounsel zaktualizowany do Llama-3) emitują te tokeny, ale kasety nie zostały przegrane. Dopóki nie przegramy kaset z aktualnymi modelami, testy będą przechodzić mimo tego buga.
