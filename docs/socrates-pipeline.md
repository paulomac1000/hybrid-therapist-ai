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

The sections below walk through each component of the Socrates pipeline, from idea through implementation.

## PITFALLS

N/A

## RELATED_DOCS

- sys.socrates-pipeline — architecture and layer responsibilities
- ref.glossary — domain terminology

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
      "output": "M|L=2|em=exhaustion|sv=moderate|ri=chronic_insomnia|cp=none",
      "wire_format": "M|L=2|em=exhaustion|sv=moderate|..."
    }
  ]
}
```

Use this to answer: which layer was slow? Which model emitted what? Did the supervisor actually consume the analyst's memo? Did the L7 retry path trigger?
