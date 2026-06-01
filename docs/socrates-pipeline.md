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

### Performatives — how the layers communicate

The pipeline uses two H.A.N.D. performatives:

**`M|` (Memo) — Analyst → Supervisor → Therapist.** The analyst emits a compact clinical report in a single line:
```text
M|L=2|e7=anxiety|s9=moderate|x4=insomnia|y1=worry
```
The supervisor reads this wire, picks an approach, and emits its own memo:
```text
M|L=3|p3=reflective_listening|t5=open_question|k2=What keeps you up at night?
```
Both memos feed directly into the therapist's prompt — raw, compact, with no expansion. The model learns to read the fields from checkpoint examples, not from a legend in the prompt.

**`R|` (Result) — Therapist → Calibrator → User.** The therapist generates a response with metadata on the first line:
```text
R|C=0.88
I hear that sleep has become a struggle for you. What tends to occupy your mind
when you lie down at night?
```
This Data/Narrative Split keeps the transformer's attention mechanism from hunting for confidence scores buried at the end of a long therapeutic response.

### Implicit Priming — teaching by example, not instruction

The models were **never told** about H.A.N.D. No system prompt says "respond in format R|C=...". Instead, before each LLM call, the orchestrator silently injects a single non-therapeutic exchange into the conversation history:

```text
User:      [SYSTEM_PROTOCOL_PING]
Assistant: R|C=1.0
           [SYSTEM_PROTOCOL_ACK]
```

The model sees the pattern and subconsciously continues it. It learns the format the same way it learns anything — by **mimicking what it sees in context**. This is a stateless cache: every call starts fresh with the same ping.

### Resilience Ladder — when small models stumble

Every layer runs the model's output through `HandResiliencePipeline` — a 5-level degradation ladder:

| Level | Strategy | What it does |
|-------|----------|--------------|
| 1 | Strict | Perfect format — passes unchanged |
| 2 | Lenient | Minor format deviations — repaired |
| 3 | Markdown Strip | Wire wrapped in ``` fences — stripped |
| 4 | Semantic Extraction | Model ignored format and wrote prose — regex extracts fields |
| 5 | Fallback | Everything failed — safe replacement memo |

This means the pipeline never throws HTTP 500 when a small model makes a mistake. Level 5 (unstructured passthrough) triggers a safe fallback memo for L2/L3, so downstream layers never see a broken input.

### Why this works — H.A.N.D. in practice

Five small models. One ~$200 GPU. Zero cloud APIs. Zero per-token billing. The models talk to each other in a pidgin language they learned by imitation — and the pipeline gracefully survives their mistakes. The therapist is the proof of concept.

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
