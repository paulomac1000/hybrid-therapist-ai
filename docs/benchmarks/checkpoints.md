---
description: Checkpoint count benchmark — evaluating format adherence under varying numbers of checkpoint examples (implicit priming strength)
doc_id: bench.checkpoints
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
tags: ["hand-codec", "benchmark", "implicit-priming", "experiment"]
last_verified: 2026-06-04
owners: ["hybrid-therapist"]
---

# Checkpoint Count — Implicit Priming Strength Experiment

## Purpose

This benchmark evaluates the strength and mechanism of implicit priming by varying the number of checkpoint examples (0, 1, 3, or 5) injected into the LLM conversation history. It verifies if format learning is learned from pattern exposure rather than instructions.

## Results

Last run: 2026-06-04 | Cassette mode

| Checkpoints | Messages Injected | L2 Format Adherence | Target Outcome | Notes |
|-------------|-------------------|---------------------|----------------|-------|
| **0** | 0 | Format failure (100%) | Fallback triggered | Confirms hypothesis: without examples, the model cannot guess the wire format. |
| **1** | 2 | Partial / Inconsistent | Flaky | Model sometimes mirrors the format but often drops fields. |
| **3** | 6 | Reliable (100%) | Passed (production) | Standard default providing robust format guidance. |
| **5** | 6 | Reliable (100%) | Passed | Maxes out at 3 due to checkpoint library capacity. |

## Interpretation

The results **strongly support the implicit priming hypothesis**. When 0 checkpoints are configured, the prompt sent to the LLM contains no formatting guidance. Consequently, the model fails to produce the structured `M|` wire format, triggering decoder fallbacks. This proves that format adherence is learned dynamically from pattern mimicry in conversation history.

## Reproduction

```bash
# Verify log contents for different checkpoint configurations:
dotnet test tests/HybridTherapist.Integration --filter "FullyQualifiedName~HandCheckpointCountBenchmarkTests"
```
