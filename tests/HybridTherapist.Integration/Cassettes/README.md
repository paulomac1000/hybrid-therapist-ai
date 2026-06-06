---
description: VCR-style cassettes recording Ollama responses for hybrid-therapist integration tests. Used by CassetteOllamaServer to run the full Socrates pipeline offline.
doc_id: ref.cassettes
type: ref
status: active
ttl_days: 365
stability: stable
ai_scope: editable
upstream:
  - sys.socrates-pipeline
  - ref.glossary
tags: ["cassettes", "integration", "testing", "ollama"]
last_verified: 2026-06-06
owners: ["hybrid-therapist"]
---

# Ollama Cassettes — Recorded Pipeline Interactions

Each cassette is a JSON file describing a sequence of Ollama `/api/chat` request/response pairs for one therapeutic scenario. Cassettes allow running the full 6-layer Socrates pipeline **offline** in CI — without a live Ollama, without a GPU, without downloading models.

## Format

```json
{
  "name": "<scenario-id>",
  "description": "<one-line summary>",
  "interactions": [
    {
      "layer": "L1_translator_pl_en",
      "request_match": {
        "model_contains": "<substring of model id>",
        "user_content_contains": "<substring of any user-message content>"
      },
      "response": {
        "model": "<model id echoed back>",
        "message": { "role": "assistant", "content": "<raw text the model would emit>" },
        "done": true
      }
    }
  ]
}
```

Matching rules in `CassetteOllamaServer`:
- The first interaction whose `model_contains` matches the request body's `model` AND `user_content_contains` matches one of the user message contents wins.
- `user_content_contains` is optional — when omitted, only `model_contains` is checked.
- Each interaction can be matched any number of times (idempotent — same request returns same response).

## Recording new cassettes

For a new scenario:

1. Run hybrid-therapist with debug logging against a live Ollama with the right models pulled:
   ```bash
   docker run --rm --name therapist-recorder \
      --network therapist-net \
     -p 8086:8080 \
     -e Ollama__BaseUrl=http://ollama:11434 \
     -e Logging__LogLevel__HybridTherapist=Debug \
     hybrid-therapist:local
   ```
2. Capture each layer's response from logs (`L1 PL→EN [sess_xxx]: ...`, or similar).
3. Build a JSON file matching the format above. Use realistic content; the wire-format `R|V=...|C=...` shape matters — the resilience pipeline reads it.
4. Add the file to `tests/HybridTherapist.Integration/Cassettes/` with a descriptive name.
5. Reference the cassette from a `[Theory]` test in `CassettePipelineTests.cs`.

## Why JSON-cassettes instead of WireMock recording

WireMock.Net's auto-record produces verbose, model-specific JSON that's hard to diff and edit. Our hand-written cassettes are:

- **Layer-keyed** — humans read "L2 analyst" rather than "request 3 of 7".
- **Match by substring** — fields like timestamps and per-request UUIDs don't matter, so we don't capture them.
- **Hand-editable** — adjust the L4 output to test echo detection without re-recording.

The trade-off: cassettes don't capture full HTTP fidelity (headers, exact streaming chunks). That's fine for our pipeline — the only thing we test is whether each layer's content survives the `TherapistHandDecoder` and shapes the next layer's input correctly.

## Currently recorded

| File | Scenario | What it tests |
|------|----------|---------------|
| `socrates-insomnia.json` | "nie mogę zasnąć od trzech tygodni" | Full 6-layer happy path, INIT phase, medium severity (insomnia heuristic), no disclaimer |
| `socrates-gratitude.json` | "dziękuję" | Short positive turn, brief layer outputs, INIT phase |
| `hand-anxiety.json` | chroniczne zamartwianie się | L2 `e7=anxiety`, L3 cognitive_restructuring approach |
| `hand-depression.json` | utrata radości i celu | L2 `e7=hopelessness`, L3 behavioral_activation |
| `hand-work-stress.json` | wypalenie zawodowe | L2 `e7=frustration_with_burnout`, L3 boundary_setting |
| `hand-insomnia.json` | 6 tygodni bezsenności | L2 chronic_insomnia, L3 sleep_hygiene |
| `hand-anger.json` | przewlekła złość | L2 `e7=anger`, L3 grounding (body awareness) |
| `hand-burnout.json` | pustka emocjonalna | L2 `s9=high` burnout, L3 grounding, depersonalization flag |
| `hand-grief.json` | żałoba po stracie rodzica | Grief topic, L3 reflective_listening, validate_emotion |
| `hand-anhedonia.json` | całkowity brak przyjemności | L2 `s9=high` anhedonia, L3 behavioral_activation |
| `hand-panic.json` | ataki paniki | L2 panic detection, L3 breathing technique |
| `hand-cognitive.json` | problemy z koncentracją | L2 cognitive_exhaustion, L3 cognitive_restructuring |
| `hand-optimistic.json` | dobry dzień — poprawa | L2 positive_shift, L3 celebrate_progress |

Crisis hard-stop (`"chcę skończyć z sobą"`) is **not** in the cassette set because it never reaches Ollama — `CrisisGate` stops at layer -1 and the canned helpline response is returned. That path is covered by `CrisisGateTests` and `TherapistFlowIntegrationTests.Crisis_HardStops_Before_Reaching_LLM`.
