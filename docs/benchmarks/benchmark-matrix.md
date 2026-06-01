---
description: Planned comparison matrix for H.A.N.D. Codec benchmark variants — plaintext, JSON, HAND v1, Codec G, Codec G Pure Implicit
doc_id: bench.benchmark-matrix
type: benchmark
status: active
rigor_tier: L1
ttl_days: 365
stability: stable
ai_scope: editable
upstream:
  - bench.hand-codec-g
  - ref.glossary
tags: ["benchmark", "comparison", "matrix", "planning"]
last_verified: 2026-06-01
owners: ["hybrid-therapist"]
---

# Benchmark Matrix — H.A.N.D. Codec Comparison Plan

## Purpose

Define the variants to be compared in structured benchmarks that evaluate H.A.N.D. Codec
against alternative inter-agent communication formats. This file serves as the research
roadmap — it lists planned experiments, not yet-executed ones.

## Variants

| ID | Variant | Description | Status |
|----|---------|-------------|--------|
| `plaintext` | Natural-language memo | L2 writes a short English paragraph: "Emotional state: anxiety. Severity: moderate..." | **planned** |
| `json` | Structured JSON memo | L2 and L3 emit JSON objects with semantic keys | **planned** |
| `hand-v1` | H.A.N.D. semantic keys | Original `em`, `sv`, `ap`, `tk`, `kq`, `rn` keys — human-readable | **planned** |
| `codec-g` | H.A.N.D. Codec G | Random keys (`e7`, `s9`, `p3`, `k2`...) — no legend in L4 prompt | **done** |
| `codec-g-pure` | Codec G Pure Implicit | Same as Codec G, plus zero L4 context-reading meta-instruction | **done** |

## Metrics per variant

| Metric | Measures |
|--------|----------|
| **Pass rate** | % of benchmark scenarios meeting quality criteria |
| **Quality score** | Heuristic score based on phrase presence/absence and response structure |
| **Fallback rate** | % of runs where any layer degraded to fallback |
| **Token count (wire)** | Characters in the `M|` memo line ÷ 3.5 (token estimate) |
| **Token count (plaintext)** | Estimated characters in an equivalent natural-language memo |
| **Token savings** | % reduction from plaintext to wire |
| **Resilience level** | Average HandResiliencePipeline recovery level (1 = perfect, 5 = full fallback) |
| **Latency (total)** | End-to-end wall clock from request to Polish response |
| **Prompt length (L4)** | Total tokens in the L4 prompt (system + memos + history + user) |

## Future experiments

### Experiment A: JSON vs H.A.N.D. Codec G

Compare structured JSON memos against Codec G on the same benchmark scenarios.

Hypothesis: Codec G achieves comparable quality with lower token use and faster
parsing, but JSON may be more robust for models without implicit priming.

### Experiment B: checkpoints count

Test how many checkpoint examples are needed for reliable implicit priming.

| Checkpoints | Expected | Status |
|------------|----------|--------|
| 0 | No format adherence | planned |
| 1 | Partial adherence | planned |
| 3 | Current result: reliable | **done** |
| 5 | Potentially more robust | planned |

### Experiment C: model swap

Substitute individual models in the pipeline to test generalizability.

| Component | Variants to test |
|-----------|-----------------|
| L2 Analyst | Mistral 7B, Llama 3 8B |
| L3 Supervisor | Mistral 7B, Gemma 2 9B |
| L4 Therapist | Llama 3 8B, Qwen 2 7B |

If Codec G works across model families, the implicit priming hypothesis is strengthened.

## When to run

| Condition | Action |
|-----------|--------|
| Codec G benchmark passes reliably (≥ 10/11 scenarios) | Run `json` and `hand-v1` comparisons |
| `json` comparison complete | Evaluate token economy and quality trade-off |
| Model swap works for at least 2/3 substitutions | Consider publishing a technical note |
| checkpoints-0 test reproduces format failure | Confirms implicit priming is the mechanism |

## Non-goals

- We are not trying to prove H.A.N.D. is universally superior.
- We are not building a production inter-agent protocol.
- We are not comparing to OpenAI function calling or MCP.
