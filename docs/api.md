---
description: OpenAI-compatible HTTP API reference for the hybrid-therapist Socrates pipeline
doc_id: ref.api-reference
type: ref
status: active
rigor_tier: L2
ttl_days: 180
stability: stable
ai_scope: editable
upstream:
  - sys.socrates-pipeline
  - ref.glossary
tags: ["api", "openai", "rest", "chat", "completions"]
last_verified: 2026-05-23
owners: ["hybrid-therapist"]
---

# Hybrid Therapist — HTTP API

## Endpoints

| Method | Path | Purpose |
|--------|------|---------|
| `POST`   | `/v1/chat/completions`     | Main therapy chat endpoint (JSON or SSE) |
| `GET`    | `/v1/models`               | List supported model IDs (LibreChat compatibility) |
| `GET`    | `/v1/trace/{sessionId}`    | Per-layer execution trace (debugging) |
| `DELETE` | `/v1/trace/{sessionId}`    | Clear trace for a session |

## Streaming (`stream: true`)

LibreChat and most OpenAI-compatible UIs send `stream: true` by default. The endpoint returns `text/event-stream` SSE chunks:

```
data: {"id":"chatcmpl-...","object":"chat.completion.chunk","created":...,"model":"hybrid-therapist","choices":[{"index":0,"delta":{"role":"assistant","content":"<full Polish response>"},"finish_reason":null}]}

data: {"id":"chatcmpl-...","object":"chat.completion.chunk","created":...,"model":"hybrid-therapist","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}

data: [DONE]
```

The Socrates pipeline is batch (not incrementally streamed) — we emit the full content in a single content chunk, followed by the finish-reason chunk and the `[DONE]` sentinel. This satisfies the OpenAI SSE contract that frontends parse.

## `POST /v1/chat/completions`

### Request

```json
{
  "model": "hybrid-therapist",
  "messages": [
    { "role": "user", "content": "Nie mogę zasnąć od trzech tygodni." }
  ]
}
```

**Required fields:**
- `model` — string, non-empty. Currently only `hybrid-therapist` is meaningful; the field is accepted as-is.
- `messages` — non-empty array of `{ role, content }`. The last `role: "user"` message is the input.

**Ignored fields (accepted for OpenAI compatibility):**
- `temperature`, `top_p`, `max_tokens`, `stream`, `stop`, `n`, `presence_penalty`, `frequency_penalty`

The flow uses per-layer temperatures (`0.1` for translation, `0.4-0.7` for generation) tuned for therapy use; client overrides are ignored.

### Response

```json
{
  "id": "chatcmpl-9f4c2c3a7b...",
  "object": "chat.completion",
  "created": 1747320000,
  "model": "hybrid-therapist",
  "choices": [
    {
      "index": 0,
      "message": {
        "role": "assistant",
        "content": "Słyszę, jak bardzo cię to wyczerpuje. Trzy tygodnie..."
      },
      "finish_reason": "stop"
    }
  ],
  "usage": { "prompt_tokens": 0, "completion_tokens": 0, "total_tokens": 0 },
  "metadata": {
    "phase": "INIT",
    "message_count": 1,
    "crisis_detected": false,
    "fallback": false
  }
}
```

**Notes:**
- `usage` is reported as zero. Per-layer token accounting is not aggregated to the response yet.
- `metadata` is a custom extension — clients can ignore it. Useful fields:

| Field | Meaning |
|-------|---------|
| `session_id`        | Stable session key (hash of first user message) |
| `trace_url`         | Path to the per-layer trace endpoint for this session |
| `phase`             | `INIT` \| `EXPLORATION` \| `DIGGING` \| `WORKING` \| `CLOSING` |
| `strategy`          | One of 10 strategies picked by `ResponseStrategySelector` |
| `severity`          | `low` \| `medium` \| `high` \| `critical` (from CrisisGate) |
| `message_count`     | Total user messages in this session |
| `topics`            | Array of canonical topics detected (`sleep`, `anxiety`, `grief`, ...) |
| `analyst_severity`  | Severity classified by L2 Analyst (may differ from CrisisGate) |
| `supervisor_approach` | Approach picked by L3 Supervisor (`CBT`, `reflective listening`, etc.) |
| `rupture_detected`  | True if user signaled the previous reply missed |
| `rupture_reason`    | `user_correction` \| `user_frustration` \| empty |
| `thematic_alignment`| True if analyst output is grounded in user input (anti-hallucination) |
| `crisis_detected`   | True if any CrisisGate signal fired (any tier) |
| `fallback`          | True if any quality gate degraded to a static apology |
| `failed_layer`      | When `fallback: true`, which layer caused the degradation (e.g. `"L4_therapist"`) |
| `error_reason`      | When `fallback: true`, the error message captured from the failed layer |

### Response headers

| Header | Value | Meaning |
|--------|-------|---------|
| `X-HT-Flow` | `hybrid-therapist` | Identifies which orchestration ran |
| `X-HT-Fallback` | `true` / `false` | True when L4 therapist call or L7 EN→PL translation degraded (fallback message returned) |

## Crisis hard-stop response

When `CrisisGate.Check()` matches a hard-stop pattern (e.g. `"chcę skończyć z sobą"`):

- Pipeline stops at layer -1. No LLM call is made.
- Response `choices[0].message.content` is the canned helpline message.
- `metadata.crisis_detected` is `true`. `metadata.crisis_severity` is `"critical"`.
- HTTP status is still `200`. The response shape is identical to a normal completion.

```json
{
  "choices": [{
    "message": {
      "role": "assistant",
      "content": "Jest mi przykro, że przechodzisz przez trudne chwile. Jako asystent AI nie mogę udzielić pomocy psychologicznej. Skontaktuj się z profesjonalistą: Telefon Zaufania dla Osób Dorosłych: 116 123 (bezpłatny, czynny 14:00-22:00) lub 112 w sytuacji zagrożenia życia."
    }
  }],
  "metadata": {
    "crisis_detected": true,
    "crisis_severity": "critical"
  }
}
```

## Errors

All errors return HTTP `400` with a body:

```json
{ "error": "Field 'messages' must be non-empty." }
```

**Error cases:**

| Status | Body | Cause |
|--------|------|-------|
| `400` | `Invalid JSON body.` | Request body did not parse as JSON |
| `400` | `Request body is required.` | Empty body |
| `400` | `Field 'model' is required.` | `model` missing or empty |
| `400` | `Field 'messages' must be non-empty.` | `messages` missing or empty array |

The pipeline itself does not throw to the HTTP layer. Internal failures (Ollama down, network errors) degrade gracefully:

- L1/L7 translator failure → static Polish fallback
- L4 therapist failure → static Polish fallback
- L6 calibrator failure → use L4 draft as-is (calibration is optional polish)

## `GET /v1/trace/{sessionId}` — debugging

Returns every layer execution for a session, in order. Captures what each layer received, what it emitted (both plain text and wire format), how long it took, and which model handled it.

```json
{
  "session_id": "sess_4fef5b90",
  "event_count": 7,
  "events": [
    {
      "timestamp": "2026-05-15T08:14:21.123Z",
      "layer": "L1_pl_en",
      "model": "SpeakLeash/bielik-minitron-7b-v3.0-instruct:Q4_K_M",
      "duration_ms": 2107,
      "outcome": "ok",
      "error": null,
      "input": "nie mogę zasnąć",
      "output": "I cannot sleep",
      "wire_format": "R|V=I cannot sleep|C=0.9"
    },
    {
      "layer": "L2_analyst",
      "model": "hf.co/mradermacher/MentaLLaMA-chat-7B-GGUF:Q4_K_M",
      "duration_ms": 5327,
      "outcome": "ok",
      "output": "M|L=2|em=exhaustion|sv=moderate|ri=insomnia|cp=none",
      "wire_format": "M|L=2|em=exhaustion|sv=moderate|ri=insomnia|cp=none"
    }
    // ...L3, L4, L6, L7
  ]
}
```

Possible `outcome` values:
- `ok` — layer succeeded
- `error` — Ollama failure or model error (see `error` field)
- `fallback_to_draft` — L6 calibrator failed, L4 draft was kept as-is
- `still_english` — L7 output failed quality gate (static fallback returned)

## `DELETE /v1/trace/{sessionId}`

Manually clear a session's trace (e.g. after debugging). Returns `204 No Content`.

## `GET /v1/models`

```json
{
  "object": "list",
  "data": [
    {
      "id": "hybrid-therapist",
      "object": "model",
      "created": 1700000000,
      "owned_by": "hybrid-therapist"
    }
  ]
}
```

Exists for LibreChat and other OpenAI-compatible UIs that probe `/v1/models` at startup.

## Example session

```bash
# 1. First contact — INIT phase, medium severity, no disclaimer
curl -s http://localhost:8080/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{"model":"hybrid-therapist","messages":[{"role":"user","content":"nie mogę zasnąć"}]}' \
  | jq '.choices[0].message.content, .metadata'

# 2. Crisis input — hard-stop, helpline returned
curl -s http://localhost:8080/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{"model":"hybrid-therapist","messages":[{"role":"user","content":"chcę skończyć z sobą"}]}' \
  | jq '.choices[0].message.content'
# → "Jest mi przykro... Telefon Zaufania: 116 123..."

# 3. List models
curl -s http://localhost:8080/v1/models | jq
```
