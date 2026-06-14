---
description: Comparison matrix for H.A.N.D. benchmark variants — plaintext, JSON, H.A.N.D. Semantic, and H.A.N.D. Compact
doc_id: bench.benchmark-matrix
type: benchmark
status: active
rigor_tier: L1
ttl_days: 365
stability: stable
ai_scope: editable
upstream:
  - bench.hand-compact
  - ref.glossary
tags: ["benchmark", "comparison", "matrix", "planning"]
last_verified: 2026-06-04
owners: ["hybrid-therapist"]
---

# Benchmark Matrix — H.A.N.D. Comparison Roadmap

## Purpose

Define the variants compared in structured benchmarks that evaluate H.A.N.D.
against alternative inter-agent communication formats. This matrix demonstrates
the efficacy of implicit priming and token economy across all formats.

## Variants

| ID | Variant | Description | Status |
|----|---------|-------------|--------|
| `hand-compact` | H.A.N.D. Compact keys | Two-character keys (e7, s9, p3, k2...) — implicit priming only | **done** |
| `hand-semantic` | H.A.N.D. Semantic keys | Human-readable keys (em, sv, ap, tk...) — same wire format | **done** |
| `plaintext` | Natural-language memo | L2/L3 emit prose paragraphs: "Emotional state: anxiety. Severity: moderate..." | **done** |
| `json` | Structured JSON memo | L2 and L3 emit JSON objects with semantic keys | **done** |

## Metrics per variant

| Metric | Measures |
|--------|----------|
| **Pass rate** | % of benchmark scenarios meeting quality criteria |
| **Quality score** | Heuristic score based on phrase presence/absence and response structure |
| **Fallback rate** | % of runs where any layer degraded to fallback |
| **Token count (wire)** | Characters in the inter-agent memo ÷ 3.5 (token estimate) |
| **Token count (plaintext)** | Estimated characters in an equivalent natural-language memo |
| **Token savings** | % reduction from plaintext to wire |
| **Resilience level** | Average HandResiliencePipeline recovery level (1 = perfect, 5 = full fallback) |
| **Latency (total)** | End-to-end wall clock from request to Polish response |
| **Prompt length (L4)** | Total tokens in the L4 prompt (system + memos + history + user) |

## Experimental Results

The benchmark suite verifies format adherence and token economy under cassette conditions:

### Experiment A: JSON/Plaintext vs H.A.N.D.

Compare structured JSON and plaintext memos against H.A.N.D. Compact and Semantic variants.

- **Hypothesis:** H.A.N.D. Compact achieves comparable quality with substantially lower token consumption compared to JSON and Plaintext, demonstrating the efficiency of compact serialization.
- **Status:** **done**

### Experiment B: Checkpoint Count

Test how many checkpoint examples are needed for reliable implicit priming.

| Checkpoints | Expected Format Adherence | Status |
|------------|---------------------------|--------|
| 0 | Format failure (fallbacks triggered) | **done** |
| 1 | Partial adherence (inconsistent) | **done** |
| 3 | Reliable adherence (production default) | **done** |
| 5 | Stable and robust adherence | **done** |

### Experiment C: Model Swap (Future Work)

Substitute individual models in the pipeline to test generalizability.

| Component | Variants to test |
|-----------|-----------------|
| L2 Analyst | Mistral 7B, Llama 3 8B |
| L3 Supervisor | Mistral 7B, Gemma 2 9B |
| L4 Therapist | Llama 3 8B, Qwen 2 7B |

## Non-goals

- We are not trying to prove H.A.N.D. is universally superior.
- We are not building a production inter-agent protocol.
- We are not comparing to OpenAI function calling or MCP.
