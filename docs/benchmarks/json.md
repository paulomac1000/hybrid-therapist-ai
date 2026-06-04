---
description: JSON benchmark — measuring token economy and formatting adherence of JSON objects for inter-agent communication
doc_id: bench.json
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
tags: ["hand-codec", "benchmark", "json"]
last_verified: 2026-06-04
owners: ["hybrid-therapist"]
---

# JSON Memo — Structured Serialization Experiment

## Purpose

This benchmark evaluates using standard JSON objects as the serialization format for inter-agent memos. It measures the overhead of JSON syntax (braces, quotes, colons, commas) compared to H.A.N.D. Compact.

## JSON Structure

- **L2 Analyst:**
  `{"layer":2,"emotional_state":"anxiety","severity":"moderate","risk":"none","patterns":"racing_thoughts","evidence":"cannot stop"}`
- **L3 Supervisor:**
  `{"layer":3,"approach":"cognitive_restructuring","technique":"thought_record","key_question":"When was the last time a worry actually came true?","risk_note":"none"}`

## Results

Last run: 2026-06-04 | Cassette mode

| Scenario | Pass | L2 Outcome | L3 Outcome | JSON Tokens | Plain Tokens | Savings % |
|----------|------|------------|------------|-------------|--------------|-----------|
| anxiety  | ✓    | ok         | ok         | 48          | 52           | 7.7%      |
| depression | ✓  | ok         | ok         | 50          | 54           | 7.4%      |
| insomnia | ✓    | ok         | ok         | 53          | 56           | 5.4%      |

**Summary: 3/3 passed. Average token savings: ~6.8%**

> [!NOTE]
> JSON provides some token savings compared to natural-language plaintext, but is significantly less efficient than H.A.N.D. Compact.

## Interpretation

JSON is a viable, standardized option, but its syntax and verbose key names carry a measurable token cost compared to the highly optimized, compact `M|` wire format.

## Reproduction

```bash
./scripts/run-hand-benchmark.sh --variant json
```
