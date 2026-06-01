---
description: Walkthrough of the Socrates pipeline wire format Implicit Priming and resilience ladder for HandCodec
doc_id: guide.socrates-pipeline
type: guide
status: active
rigor_tier: L1
ttl_days: 180
stability: stable
ai_scope: editable
upstream:
  - sys.socrates-pipeline
  - ref.glossary
tags: ["socrates", "handcodec", "wire-format", "implicit-priming", "resilience"]
last_verified: 2026-05-23
owners: ["hybrid-therapist"]
---

# Socrates Multi-Agent Pipeline

## PURPOSE

Walkthrough of the Socrates pipeline: wire-format protocol, Implicit Priming negotiation, and the 5-level resilience ladder for parsing LLM outputs with HandCodec.

## AUDIENCE

Developers integrating with HandCodec, contributors to the hybrid-therapist pipeline, and AI agents maintaining the codebase.

## CONTEXT

The hybrid-therapist uses 6 local LLMs behind HandCodec to deliver a therapy session. This guide explains the design decisions, wire-format semantics, and failure-degradation strategy that make the pipeline reliable despite small-model noise.

## WALKTHROUGH

### Performatives — jak warstwy ze sobą rozmawiają

Pipeline używa dwóch performatywów H.A.N.D.:

**`M|` (Memo) — Analityk → Supervisor → Terapeuta.** Analityk emituje zwięzły raport kliniczny w jednej linii:
```
M|L=2|e7=anxiety|s9=moderate|x4=insomnia|y1=worry
```
Supervisor odczytuje ten wire, wybiera podejście i emituje własne memo:
```
M|L=3|p3=reflective_listening|t5=open_question|k2=What keeps you up at night?
```
Oba mema trafiają bezpośrednio do promptu terapeuty — surowe, skompresowane, bez rozwijania. L4 nie dostaje legendy kluczy; wzorzec `M|` pochodzi z checkpointów w historii konwersacji.

**`R|` (Result) — Terapeuta → Kalibrator → Użytkownik.** Terapeuta generuje odpowiedź z metadanymi w pierwszej linii:
```
R|C=0.88
Słyszę, że sen stał się dla Ciebie walką. Co zaprząta Ci myśli,
kiedy kładziesz się spać?
```
Ten podział na dane i narrację (Data/Narrative Split) zapobiega polowaniu uwagi transformera na wyniki konfidencji ukryte na końcu długiej odpowiedzi terapeutycznej.

### Implicit Priming — uczenie przez przykład, nie przez instrukcję

Modele **nigdy nie dostały instrukcji** o formacie H.A.N.D. Żaden system prompt nie mówi "odpowiadaj w formacie R|C=...". Zamiast tego, przed każdym wywołaniem LLM, orkiestrator po cichu wstrzykuje jedną nieterepeutyczną wymianę do historii konwersacji:

```
User:      [SYSTEM_PROTOCOL_PING]
Assistant: R|C=1.0
           [SYSTEM_PROTOCOL_ACK]
```

Model widzi wzorzec i podświadomie go kontynuuje. Uczy się formatu tak samo jak wszystkiego innego — **naśladując to, co widzi w kontekście**. To jest bezstanowy cache: każde wywołanie zaczyna się od nowa z tym samym pingiem.

### Drabina odporności — gdy małe modele się potykają

Każda warstwa przepuszcza output modelu przez `HandResiliencePipeline` — 5-stopniową drabinę degradacji:

| Poziom | Strategia | Co robi |
|--------|-----------|---------|
| 1 | Strict | Format idealny — przechodzi bez zmian |
| 2 | Lenient | Drobne odstępstwa od formatu — naprawiane |
| 3 | Markdown Strip | Wire owinięty w ``` fences — ściągane |
| 4 | Semantic Extraction | Model zignorował format i napisał prozą — regex wyciąga pola |
| 5 | Fallback | Wszystko zawiodło — bezpieczne memo zastępcze |

Dzięki temu pipeline nie rzuca HTTP 500 gdy mały model się pomyli. Poziom 5 (nieustrukturyzowany pass-through) wyzwala bezpieczne memo awaryjne dla L2/L3, więc warstwy downstream nigdy nie widzą zepsutego wejścia.

### Dlaczego to działa — H.A.N.D. w praktyce

Pięć małych modeli. Jedna karta graficzna za ~200 USD. Zero API w chmurze. Zero opłat za token. Modele rozmawiają ze sobą w języku pidgin, którego nauczyły się przez naśladownictwo — a pipeline przeżywa ich błędy z gracją. Terapeuta jest dowodem koncepcji.

## What this architecture buys vs single-model

Empirically observed:

- **Fewer wooden openings.** L6 catches them.
- **Less echoing.** L4 explicit "never repeat" + L6 editorial pass beats single-pass.
- **Better phase coherence.** Phase prompt at L4 + phase awareness in L3 strategy.
- **Crisis sensitivity.** Three model layers see the message; any can flag `S=crisis`.
- **No hallucinated themes.** Thematic alignment catches fabrications from L2.
- **No wrong-language outputs.** L7 quality gate (still-English detection) + Polish QA gate.

Cost: 6 sequential Ollama calls per turn (~50-90s on GTX 1060 with model swaps). Acceptable for a therapy chat where turn latency budget is 30-90s. The L5 summarizer adds occasional cost but keeps prompt sizes bounded.

## Debugging — the trace endpoint

```bash
SESSION_ID="sess_4fef5b90"   # from response metadata.session_id
curl http://localhost:8080/v1/trace/$SESSION_ID | jq
```

Returns a JSON document with one event per layer call:

```json
{
  "session_id": "sess_4fef5b90",
  "event_count": 7,
  "events": [
    {
      "timestamp": "2026-05-15T...",
      "layer": "L2_analyst",
      "model": "hf.co/mradermacher/MentaLLaMA-chat-7B-GGUF:Q4_K_M",
      "duration_ms": 5327,
      "outcome": "ok",
      "input": "(prompt truncated to 2000 chars)",
      "output": "M|L=2|e7=exhaustion|s9=moderate|x4=chronic_insomnia|y1=none",
      "wire_format": "M|L=2|e7=exhaustion|s9=moderate|..."
    }
  ]
}
```

Use this to answer: which layer was slow? Which model emitted what? Did the supervisor actually consume the analyst's memo? Did the L7 retry path trigger?
