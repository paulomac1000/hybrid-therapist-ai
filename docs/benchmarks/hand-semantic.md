---
description: H.A.N.D. Semantic benchmark — implicit priming experiment testing whether small LLMs adhere to a human-readable key-value protocol variant
doc_id: bench.hand-semantic
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
tags: ["hand-codec", "benchmark", "hand-semantic", "implicit-priming"]
last_verified: 2026-06-04
owners: ["hybrid-therapist"]
---

# H.A.N.D. Semantic — Implicit Priming Experiment

## Purpose

This benchmark evaluates the performance of the **H.A.N.D. Semantic** variant, which uses human-readable semantic keys (`em`, `sv`, `ri`, `cp`, `ev` for L2 Analyst and `ap`, `tk`, `kq`, `rn` for L3 Supervisor) instead of two-character compact keys. It is used to evaluate the trade-off between key readability and token efficiency.

## Key Mapping

The semantic keys translate directly to compact keys as follows:

| Semantic key | Compact key | Meaning |
|--------------|-------------|---------|
| `em` | `e7` | emotional state |
| `sv` | `s9` | severity |
| `ri` | `x4` | risk indicators |
| `cp` | `y1` | cognitive patterns |
| `ev` | `q3` | evidence quote |
| `ap` | `p3` | approach |
| `tk` | `t5` | technique |
| `kq` | `k2` | key question |
| `rn` | `r8` | risk note |

## Results

Last run: 2026-06-04 | Cassette mode

| Scenario | Pass | L2 Outcome | L3 Outcome | Wire Tokens | Plain Tokens | Savings % |
|----------|------|------------|------------|-------------|--------------|-----------|
| anxiety  | ✓    | ok         | ok         | 48          | 52           | 7.7%      |
| depression | ✓  | ok         | ok         | 52          | 54           | 3.7%      |
| insomnia | ✓    | ok         | ok         | 55          | 56           | 1.8%      |

**Summary: 3/3 passed. Average token savings: ~4.4%**

> [!NOTE]
> Since the keys are human-readable, they consume more characters. Consequently, token savings are lower than the H.A.N.D. Compact variant (which averages ~24% savings).

## Interpretation

The model stack successfully adheres to the H.A.N.D. Semantic keys structure through implicit priming, indicating that semantic information in key names does not disrupt protocol learning. However, the token economy is significantly degraded due to the overhead of longer key names.

## Reproduction

```bash
./scripts/run-hand-benchmark.sh --variant semantic
```
