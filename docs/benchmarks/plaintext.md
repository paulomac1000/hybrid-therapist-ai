---
description: Plaintext benchmark — measuring the token overhead and formatting trade-offs of using natural-language prose instead of a wire format
doc_id: bench.plaintext
type: benchmark
status: active
rigor_tier: L1
ttl_days: 365
stability: stable
ai_scope: review_only
source_of_truth: true
upstream:
  - sys.socrates-pipeline
  - ref.glossary
tags: ["hand-codec", "benchmark", "plaintext"]
last_verified: 2026-06-06
owners: ["hybrid-therapist"]
---

# Plaintext Memo — Token Overhead Experiment

## Purpose

This benchmark variant measures the baseline token consumption when agents communicate using natural-language prose paragraphs instead of a structured wire format. L2 Analyst emits a clinical analysis paragraph, L3 Supervisor emits a supervisor analysis paragraph, and L4 Therapist processes these plaintext paragraphs directly.

## Results

Last run: 2026-06-06 | Cassette mode

| Scenario | Pass | L2 Outcome | L3 Outcome | Plain Tokens | Compact Equivalent | Savings % |
|----------|------|------------|------------|--------------|-------------------|-----------|
| anxiety  | ✓    | ok         | ok         | 75           | 35                | -114.3%   |
| depression | ✓  | ok         | ok         | 91           | 35                | -160.0%   |
| insomnia | ✓    | ok         | ok         | 94           | 35                | -168.6%   |

**Summary: 3/3 passed. Average token savings: ~-147.6%**

> [!IMPORTANT]
> The negative token savings indicate that plaintext communication carries significant token overhead compared to the H.A.N.D. Compact format (consuming ~148% more tokens on average for the inter-agent memo).

## Interpretation

While natural-language plaintext requires no custom parser and is highly readable, it is the most expensive format in terms of token economics. As context size grows, this overhead compounds, making structured compression highly beneficial for local, resource-constrained LLM pipelines.

## Reproduction

```bash
./scripts/run-hand-benchmark.sh --variant plaintext
```
