---
description: H.A.N.D. Compact benchmark — implicit priming experiment testing whether small LLMs learn an arbitrary compact inter-agent wire format from checkpoint examples alone
doc_id: bench.hand-compact
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
tags: ["hand-codec", "benchmark", "hand-compact", "implicit-priming", "experiment"]
last_verified: 2026-06-04
owners: ["hybrid-therapist"]
---

# H.A.N.D. Compact — Implicit Priming Experiment

## Purpose

This benchmark evaluates whether small local LLMs (7B–8B) can use an arbitrary,
compact inter-agent wire format — **H.A.N.D. Compact**, with randomly renamed keys that
carry no semantic meaning — without explicit format instructions in system prompts.

## Hypothesis

> If L2, L3 and L4 can communicate using H.A.N.D. Compact keys (`e7`, `s9`, `x4`, `p3`, `t5`, `k2`...)
> after seeing only checkpoint examples, then the pipeline demonstrates **implicit
> protocol learning** — the models learn an emergent micro-language from pattern
> exposure rather than keyword semantics.

## What is being tested

- **L2 Analyst** receives a user message and generates a H.A.N.D. Compact `M|` clinical memo.
- **L3 Supervisor** reads the L2 memo and generates a H.A.N.D. Compact `M|` supervisor memo.
- **L4 Therapist** reads both raw `M|` memo lines and the user message, then generates
  a therapeutic response — **without any legend, schema, or key explanation in its prompt.**
- Final Polish output remains therapeutically coherent.

## What is NOT being tested

- Clinical safety or therapeutic efficacy.
- Whether H.A.N.D. is superior to JSON, YAML, natural-language memos, or any other serialization format.
- Generalization beyond this specific model stack (MentaLLaMA / PsyLLM / PsychoCounsel).
- Whether the benchmark replaces human therapeutic evaluation.
- Performance across different hardware, quantization levels, or prompt templates.

## Experimental setup

| Component | Detail |
|-----------|--------|
| **L2 Analyst** | MentaLLaMA 7B (Q4_K_M) — clinical emotional analysis |
| **L3 Supervisor** | PsyLLM 8B (Q4_K_M) — therapeutic strategy selection |
| **L4 Therapist** | PsychoCounsel 8B (Q4_K_S) — response generation |
| **L1 / L7 Translator** | Bielik 7B (Q4_K_M) — Polish↔English |
| **L6 Calibrator** | Llama4-Dolphin 8B (Q4_K_S) — editorial polish |
| **Runtime** | Ollama, local only, zero cloud APIs |
| **Checkpoints per layer** | 3 diverse examples via `TherapyAnalystPing` / `TherapySupervisorPing` |
| **L4 system prompt** | Pure therapeutic instruction — no legend, no `` `M\|` `` mention, no key explanation |
| **Strict mode** | `TokenSavingsTracker.StrictCodecG = true` — no verbose-key fallback |

## H.A.N.D. Compact key mapping

H.A.N.D. Compact replaces semantic key names with arbitrary two-character identifiers.
The model never sees the semantic names — only the checkpoints teach the pattern.

| Semantic meaning | H.A.N.D. Compact key |
|------------------|----------------------|
| emotional state  | `e7`                 |
| severity         | `s9`                 |
| risk indicators  | `x4`                 |
| cognitive patterns | `y1`               |
| evidence quote   | `q3`                 |
| approach         | `p3`                 |
| technique        | `t5`                 |
| key question     | `k2`                 |
| risk note        | `r8`                 |
| session goal     | `g6`                 |
| crisis flag      | `f0`                 |

Example L2 output (H.A.N.D. Compact):
```
M|L=2|e7=fatigued|s9=low|x4=none|y1=exhausted|q3="so tired"
```

Example L3 output (H.A.N.D. Compact):
```
M|L=3|p3=behavioral_activation|t5=short_walk|k2=What_small_move_feels_possible_today?|r8=none
```

## Prompt purity

The L4 therapist system prompt contains **only** therapeutic instruction:

```
You are an empathetic therapist. Respond with warmth and clinical insight.
Do not give direct advice. Ask one open question to continue.
```

The following are **absent** from the L4 prompt (verified by `ImplicitPrimingTests`):

- `M|` mention
- `memo` / `wire` / `format` / `key` / `Codec` / `structured`
- `em=`, `sv=`, `ap=`, `e7=`, `s9=`, `p3=` or any key-value hint
- any instruction telling L4 to use provided context below
- any instruction telling L4 to read `M|` memo messages
- `Analyst memo keys:` / `Supervisor memo keys:`

The model sees the `M|` format exclusively through checkpoint examples in conversation history.

## Benchmark scenarios

Each scenario is a WireMock cassette (`hand-*.json`) simulating a full 6-layer
Socrates pipeline with recorded Ollama responses in H.A.N.D. Compact format.

| Scenario | User profile | Target detection |
|----------|-------------|-----------------|
| `hand-anxiety` | Constant worry, racing thoughts | L2 anxiety, L3 cognitive_restructuring |
| `hand-depression` | Loss of joy and purpose | L2 hopelessness, L3 behavioral_activation |
| `hand-work-stress` | Workplace burnout | L2 burnout, L3 boundary_setting |
| `hand-insomnia` | 6 weeks without sleep | L2 chronic insomnia, L3 sleep_hygiene |
| `hand-anger` | Chronic irritability | L2 anger, L3 grounding |
| `hand-burnout` | Emotional emptiness, depersonalization | L2 `s9=high`, L3 grounding |
| `hand-grief` | Loss of a parent | L2 grief, L3 reflective_listening |
| `hand-anhedonia` | Complete loss of pleasure | L2 `s9=high` anhedonia, L3 behavioral_activation |
| `hand-panic` | Panic attacks with physical symptoms | L2 panic, L3 breathing |
| `hand-optimistic` | "Today is a good day" — positive shift | L2 positive_shift, L3 celebrate_progress |
| `hand-cognitive` | Memory and concentration problems | L2 cognitive_exhaustion, L3 cognitive_restructuring |

## Pass criteria

A scenario passes when **all** of these hold:

1. L2 emits a valid H.A.N.D. Compact memo (`M|L=2|e7=...|s9=...|...`).
2. L3 emits a valid H.A.N.D. Compact memo (`M|L=3|p3=...|t5=...|k2=...|...`).
3. L4 trace input contains the raw L2 and L3 H.A.N.D. Compact memos (`M|L=2|...` and `M|L=3|...`).
4. L4 system prompt remains pure therapeutic instruction: no legend, no key names, no format instruction.
5. No fallback is triggered (`metadata.fallback = false`).
6. Every `expected_quality.required_topics` entry is present in `metadata.topics`.
7. Every required Polish phrase is present in the final response.
8. Every forbidden phrase is absent.
9. The final response passes `LooksPolish`: not mostly English, contains Polish markers, has reasonable length, and contains an open question.

These are hard assertions in strict mode. A quality score may be reported as secondary
metadata, but it does not make a failing scenario pass.

## Results

Current results are generated by the benchmark runner and written to
`artifacts/benchmarks/hand-compact-latest.json` and
`artifacts/benchmarks/hand-compact-latest.md`.

```bash
./scripts/run-hand-benchmark.sh --variant compact --report
```

The documentation intentionally does not hardcode the current pass count or token
savings. If a run does not emit token-economy measurements, the report records
`"token_savings_status": "not_measured"` and uses `null` numeric values.

### Example historical live trace

The following trace is an illustrative live run captured during development. It is
not the source of truth for current benchmark results.

L2 Analyst (MentaLLaMA 7B) generated:
```
M|L=2|e7=fatigued|s9=low|x4=none|y1=exhausted|q3="so tired"
```

L3 Supervisor (PsyLLM 8B) generated:
```
M|L=3|p3=behavioral_activation|t5=short_walk|k2=What_small_move_feels_possible_today?|r8=none
```

L4 Therapist (PsychoCounsel 8B) received the raw `M|` lines with **zero key explanation**
in its prompt and produced a therapeutically relevant response about sleep.

### Token economy

Token economy is measured from inter-agent memo wire, not from the final user-facing
response:

```
wire = L2_wire + L3_wire
plaintext = expanded_plaintext(L2_wire) + expanded_plaintext(L3_wire)
savings = 1 - wire_tokens / plaintext_tokens
```

The runner parses runtime measurements from benchmark output. It does not use
hardcoded savings values.

## Interpretation

The result **suggests** that the models are not relying on human-readable key names.
The arbitrary `e7`/`s9`/`p3`/`k2` keys carry no inherent semantic meaning — yet all three
models successfully generated and consumed H.A.N.D. Compact memo lines.

This **supports the hypothesis** that H.A.N.D. Compact can serve as a compact inter-agent
pidgin language, learned through implicit priming from checkpoint examples alone.

Key observations:

- The models followed the `key=value|key=value` positional structure, not the key meanings.
- No model "broke format" by emitting old semantic keys (`em`, `sv`, `ap`).
- L4 consumed the memos without any format instruction and produced a coherent response.

This **demonstrates in this model stack** that small local models (7B–8B parameters)
can negotiate an arbitrary wire protocol through pattern exposure in conversation history.

## Limitations

1. **Curated scenarios.** The 11 benchmark cassettes are hand-written, not randomly sampled.
   They cover common therapeutic themes but do not represent the full distribution of user inputs.

2. **Heuristic quality assertions.** Pass/fail is based on trace structure, topics,
   phrase presence/absence and language checks, not clinical evaluation. A response
   matching required phrases may still be therapeutically poor.

3. **No human evaluation.** No therapist has reviewed the benchmark outputs for clinical
   appropriateness.

4. **No comparison baseline.** H.A.N.D. Compact has not yet been compared to plaintext memos, JSON memos,
   YAML memos, or natural-language bullet memos. The experiment shows H.A.N.D. Compact *works* but does
   not claim it *outperforms* alternatives.

5. **Model-stack specific.** Results were obtained with MentaLLaMA 7B, PsyLLM 8B, and
   PsychoCounsel 8B. Different models may show different implicit priming behaviour.

6. **Tokenizer approximation.** Token counts are character-based estimates (1 token ≈ 3.5 chars),
   not exact tokenizer output. Absolute savings may vary.

7. **Checkpoint diversity.** Each layer sees exactly 3 checkpoint examples. Smaller or larger
   checkpoint sets may yield different results.

8. **No sustained multi-turn.** Benchmarks test single-turn interactions. Multi-turn sessions
   may degrade protocol adherence over time.

## Reproduction

```bash
# Cassette benchmark: no Docker, no Ollama, suitable for CI
./scripts/run-hand-benchmark.sh --variant compact --report

# Or manually for cassette:
dotnet build HybridTherapist.sln -c Release
dotnet test tests/HybridTherapist.Tests -c Release
dotnet test tests/HybridTherapist.Integration -c Release --filter "HandBenchmark"
```

The benchmark script produces structured output in `artifacts/benchmarks/`.

## Related documents

- [benchmark-matrix.md](benchmark-matrix.md) — planned comparison variants
- [../architecture.md](../architecture.md) — full pipeline architecture
- [../socrates-pipeline.md](../socrates-pipeline.md) — wire format and Implicit Priming details
- [../meta/glossary.md](../meta/glossary.md) — domain terminology
