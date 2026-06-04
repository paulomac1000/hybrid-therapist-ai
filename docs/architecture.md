---
description: Architecture of the 17-layer Socrates multi-agent therapy pipeline with HandCodec wire format integration
doc_id: sys.socrates-pipeline
type: system
status: active
rigor_tier: L2
ttl_days: 90
stability: stable
ai_scope: editable
source_of_truth: true
upstream:
  - ref.glossary
tags: ["architecture", "socrates", "handcodec", "pipeline"]
last_verified: 2026-05-23
owners: ["hybrid-therapist"]
---

# Architecture

## Overview

```
                    HTTP /v1/chat/completions
                              │
                              ▼
                       ┌──────────────┐
                       │  ChatEndpoint│  validate request, route to flow
                       └──────┬───────┘
                              ▼
                       ┌──────────────┐
                       │ TherapistFlow│  orchestrates 17 layers
                       └──────┬───────┘
        ┌─────────────────────┼─────────────────────┐
        ▼                     ▼                     ▼
   CrisisGate          PrivacySanitizer       InMemoryState
   PrivacySanitizer    (PII redaction)       (per-session)
   (regex PL/EN)
                              │
                              ▼
                  TherapistLayerService          HandCodec
        ┌─────────────────────┼─────────────────────┐
        ▼                     ▼                     ▼
   OllamaAdapter      AnalystLayer          M| Memo wire format
   (local-only)       SupervisorLayer       between L2→L3→L4
                      TherapyMemoryService
                              │
                              ▼
                       ┌──────────────┐
                       │  ITraceSink  │  per-layer trace
                       └──────┬───────┘
                              ▼
                  GET /v1/trace/{sessionId}
```

Five .NET projects:

| Project | Responsibility |
|---------|----------------|
| `HybridTherapist.Domain` | Interfaces, models, `SessionPhase`, `TopicRegistry`, `RuptureDetector`, `ThematicAlignment`, `ResponseStrategySelector`, `QualityValidator` |
| `HybridTherapist.Security` | `CrisisGate`, `PrivacySanitizer` — must run before any LLM call |
| `HybridTherapist.Infrastructure` | `OllamaAdapter`, `InMemoryTherapyStateRepository`, `InMemoryTraceSink` |
| `HybridTherapist.Application` | `TherapistFlow`, `TherapistLayerService`, `AnalystLayer`, `SupervisorLayer`, `TherapyMemoryService`, `TherapistHandEncoder/Decoder` |
| `HybridTherapist.Api` | ASP.NET Core host, `/v1/chat/completions` (JSON + SSE), `/v1/models`, `/v1/trace/{sessionId}`, DI wiring |

External dependencies:
- `HandCodec` — wire format codec (pipe-delimited `key=value`), Resilience Ladder, MemoBuilder
- `HandRuntime` — Implicit Priming orchestration (ConversationBuilder, CheckpointLibrary, WireConvention, ResponseDecoder)

## Zero cloud — all local

The Socrates pipeline runs **entirely on local Ollama**. No OpenRouter. No external APIs. All LLM calls go to `http://ollama:11434/api/chat`. Translator quality is safeguarded by a quality gate: Bielik 7B, single pass → static Polish fallback if output still looks like English.

## 17-warstwowy pipeline Socrates

```
Layer  Name                  Implementation                                       Feeds downstream
─────  ─────────────────────  ───────────────────────────────────────────────────  ───────────────────
 -1   CrisisGate              regex PL/EN, 200ms timeout → helpline 116 123        short-circuits flow
  0   PrivacySanitizer        email, phone, PESEL, full-name redaction             sanitized → L1
  1   StateLoader             InMemoryTherapyStateRepository                       state object
  2   TopicExtraction         TopicRegistry — PL/EN keyword → canonical topic     → L2 prompt + metadata
  3   PhaseMachine            INIT → EXPLORATION → DIGGING → WORKING → CLOSING    → L4 phase guidance + strategy
  4   RuptureDetector         user-correction regex → force Repair strategy        → ResponseStrategy
  5   ResponseStrategy        phase × severity × rupture → 1 of 10 strategies     → L3 Supervisor
  6   L1 PL→EN                Bielik 7B (GPU)                                     English text → L2/L3/L4
   7   L2 Analyst              MentaLLaMA 7B generates native M|L=2 Memo via       → ThematicAlignment + L3
                               Implicit Priming (MemoPing checkpoint)
   8   ThematicAlignment       null memo if analyst fabricated sensitive themes     redacted memo → L3
   9   L3 Supervisor           PsyLLM 8B generates native M|L=3 Memo via           → L4 (raw wire, no expansion)
                               Implicit Priming (MemoPing checkpoint)
  10   L5 MemoryService        every 8 msg OR phase change → summary + truncate    state.SessionSummary → L4
  11   L4 Therapist            PsychoCounsel 8B reads both raw M| memos +          EN draft → L6
                               pure therapeutic system prompt + summary +
                               SessionPhase.GetPhaseSystemPrompt(phase)
 12   L6 Calibrator           Llama4-Dolphin 8B polishes draft                    EN polished → QA1
 13   QualityValidator (QA1)  EN-side: echo, length, prompt-leakage check         pass-through or L4 draft
 14   L7 EN→PL                Bielik 7B with quality gate                          Polish text → QA2
 15   PolishQualityCheck (QA2) final language + echo check                        pass-through or static fallback
 16   Disclaimer              phase-aware (INIT + medium severity → skip helpline) appended to PL
 17   Audit                   InMemoryTraceSink + structured Serilog              /v1/trace/{sessionId}
```

**Security invariant**: layers -1 and 0 must execute before any LLM call. Hard-stopped at layer -1 means the LLM is never invoked.

**Necessity invariant**: every layer has a corresponding test in `LayerNecessityTests` that fails if the layer's contribution is bypassed. No layer is "dead weight" — each one demonstrably shapes the output, blocks unsafe data, or enables a downstream consumer.

### Layer taxonomy — why 17 is not "too many"

The 17-layer count is misleading if read as "17 sequential LLM calls". In reality, only **6 layers** invoke an LLM (each costing 5-15s). The remaining 11 are sub-millisecond in-process operations:

| Category | Layers | Runtime cost | Purpose |
|----------|--------|--------------|---------|
| **Safety Guards** | L-1 CrisisGate, L0 PrivacySanitizer | < 1ms each, **mandatory** | Crisis hard-stop + PII redaction — these are never optional |
| **State & Strategy** | 1-5 (StateLoader, TopicExtraction, PhaseMachine, RuptureDetector, ResponseStrategy) | < 1ms each | In-memory lookups, regex, enum selection |
| **LLM Pipeline** | L1 PL→EN, L2 Analyst, L3 Supervisor, L4 Therapist, L6 Calibrator, L7 EN→PL | 5-15s each | The actual multi-agent orchestration core |
| **QA Gates** | QA1, QA2, ThematicAlignment | < 1ms each | Output validation — echo, leakage, language checks |
| **Post-processing** | Disclaimer, Audit | < 1ms each | Conditional append + structured logging |

The lightweight layers ARE the production hardening. Removing CrisisGate or PrivacySanitizer would be a safety regression, not a simplification.

## Data flow between layers — what each layer actually feeds

| Source | Carries | Consumer | Purpose |
|--------|---------|----------|---------|
| TopicRegistry | `state.Topics` (canonical ids) | L2 Analyst prompt | Grounds the analyst — model knows "user has been discussing: sleep, anxiety" |
| TopicRegistry | `metadata.topics` | API response | Visibility for dashboards / debugging |
| PhaseMachine | `state.CurrentPhase` (string) | L2/L3/L4 prompts | Phase-aware framing |
| PhaseMachine + SessionPhase | `GetPhaseSystemPrompt(phase)` | L4 system prompt | "this is first contact" vs "session winding down" |
| RuptureDetector | `Result.Detected` (bool) | ResponseStrategySelector | Forces `Repair` strategy regardless of phase |
| ResponseStrategy | enum value | L3 Supervisor system prompt | Supervisor tailors approach |
| L2 Analyst | `M\|L=2\|e7=...\|s9=...` wire | L3 Supervisor + ThematicAlignment | Structured emotional state (raw wire, parsed by HandParser) |
| ThematicAlignment | bool + redacted memo | L3 Supervisor | Anti-hallucination |
| L3 Supervisor | `M\|L=3\|p3=...\|t5=...\|k2=...` wire | L4 Therapist | Structured therapeutic plan (raw wire) |
| L5 MemoryService | `state.SessionSummary` (text) | L4 Therapist prompt | Long-term context across history compaction |
| L4 Therapist | EN draft | L6 Calibrator | Source of truth for facts |
| QualityValidator | verdict (ok/echo/leak/...) | flow control | EN-side gate before L7 |
| L6 Calibrator | EN polished | L7 Translator | Final EN before language flip |
| L7 EN→PL | Polish text | QA2 | Source of user-facing reply |
| PolishQualityCheck | verdict | flow control | PL-side gate before disclaimer |
| ITraceSink | TraceEvent per layer | /v1/trace/{sessionId} | Debugging |

## HandCodec + HandRuntime integration pattern

### Extension methods for domain-specific semantics

`MemoBuilder` in `HandCodec` is 100% domain-agnostic — it exposes only `.Field(key, value)`. Domain-specific semantic method names (`.EmotionalState()`, `.Severity()`, `.Approach()`, `.KeyQuestion()`, and other domain methods) are provided as **C# extension methods** in the consuming application. This keeps the core codec free of domain vocabulary while maintaining a fluent, domain-aware API surface at the application layer.

See `HybridTherapist.Application/Hand/TherapistMemoBuilderExtensions.cs` for the complete set of therapy-domain extension methods.

### Facade pattern for HandRuntime

The `HybridTherapist.Application/Hand/` directory contains thin **facade classes** that delegate to `HandRuntime` types:

| Facade file | Delegates to (HandRuntime) |
|---|---|
| `HandConversationBuilder.cs` | `HandRuntime.ConversationBuilder` |
| `HandResponseDecoder.cs` | `HandRuntime.ResponseDecoder` |
| `HandCheckpointLibrary.cs` | `HandRuntime.CheckpointLibrary` |

This pattern allows the application to inject domain-specific configuration (e.g., the Polish CrisisKeywordDetector via `HandResilientOptions.CrisisDetector`) while delegating all protocol-level logic to the domain-agnostic runtime.

The `RuntimeAliases.cs` file re-exports HandRuntime types under their short names for ergonomic use within the application — avoiding `.HandRuntime.` prefix pollution in pipeline code.

L2 (Analyst) and L3 (Supervisor) communicate natively via the `M|` (Memo) performative.
Both models generate `M|` wire directly through **Implicit Priming** — they learn the format
from a single `[SYSTEM_PROTOCOL_PING]` exchange in their conversation history (MemoPing checkpoint),
never from instruction in the system prompt.

```
L2 emits:  M|L=2|e7=anxiety|s9=moderate|x4=insomnia,worry|y1=catastrophizing
L3 reads:  raw M| wire parsed via HandParser → injected directly into L3 prompt as [ANALYST MEMO]
L3 emits:  M|L=3|p3=reflective_listening|t5=open_question|k2=What keeps you up?
L4 reads:  both raw M| wires as [ANALYST MEMO] and [SUPERVISOR MEMO] blocks
```

**Key architectural change (May 2026):** `MemoToPlainText()` has been **removed**.
Raw `M|` wire enters L3 and L4 prompts directly. The old invariant — "M| never enters
a model-facing prompt" — is replaced by: **"M| enters prompts as raw wire; checkpoint
examples teach the compact field pattern."**

This saves ~120 tokens per turn by eliminating the plaintext expansion step.
The compact `M|` format reduces prompt context consumed by downstream transformers.

### R| Result performative — Data/Narrative Split

L1, L4, and L7 use the `R|` (Result) performative, primed via a SystemPing checkpoint:

```
L4 emits:  R|C=0.90
           Three weeks of broken sleep wears down both body and mind...
```

Short values (translations, single words) use `V=` in the header line.
Long prose (therapeutic responses, multi-sentence translations) goes to **Body**
(the line after the wire header). The wire convention (via `HandRuntime.HandWireConvention.PrefillFor`)
appends `R|C=` as an assistant-turn prefill for `AgentClass.Assisted` models.

### Resilience Ladder

Every LLM layer decodes model output via `HandResiliencePipeline.Parse()` (levels 1-5).
The resilience level is logged as `[Drabina] L{N} resilience level {Level}` for monitoring.
Level 5 (unstructured passthrough) triggers a safe fallback memo for L2/L3 so downstream
layers never see a broken input.

## Anti-hallucination guard

`ThematicAlignment.Verify()` runs after L2 Analyst. If the analyst's report mentions a **sensitive theme** (betrayal, abuse, trauma, suicide, grief, addiction) that has **no supporting signal** in the user's input, the memo is replaced with a safe minimal version before reaching L3. Without this, a small analyst model can hallucinate "betrayal" from a sleep complaint and the entire downstream chain runs on a fabricated premise.

## Quality gates

Two QA stages run after the language-generating layers:

1. **English-side QA** (after L6 Calibrator) — catches echo of user input, too-short responses, and prompt template leakage (`confidence_decimal`, `[ANALYST CONTEXT]`, and other template artifacts).
2. **Polish-side QA** (after L7 Translator) — verifies the output is actually Polish (diacritic ratio), wasn't an echo, doesn't contain wire-format remnants.

Failure of either gate triggers a fallback: EN-side falls back to L4 draft, PL-side falls back to a static apology message.

## Per-layer tracing

Every layer call records a `TraceEvent` to `ITraceSink`. Fetch with:

```bash
curl http://localhost:8080/v1/trace/{sessionId}
```

Each event contains: timestamp, layer name, model used, duration_ms, outcome (ok/error/retry_still_english/...), input (truncated), output, wire_format. Use this to debug why a particular response came out the way it did.

The session_id is returned in `metadata.session_id` of every chat-completion response.

## Layer-by-layer responsibilities

### L2 Analyst — observation only

Generates a native `M|` Memo wire via Implicit Priming (MemoPing checkpoint).
The system prompt teaches the field dictionary; the model emits a single line:
```text
M|L=2|e7=exhaustion_with_anxiety|s9=moderate|x4=chronic_insomnia|y1=catastrophizing
```
Output decoded by `HandResiliencePipeline` (levels 1-5). Level 5 triggers a safe
fallback memo (`M|L=2|e7=unknown|s9=low|note=decoder_level5_fallback`).

### L3 Supervisor — strategy only

Receives the analyst's raw `M|` memo (as `[ANALYST MEMO]`) plus the strategy enum
picked by phase × severity. Generates a native `M|` Memo via Implicit Priming:
```text
M|L=3|p3=reflective_listening|t5=open_question|k2=What_keeps_you_up_at_night?|r8=none
```
Raw `M|` wire passes directly to L4. **The supervisor never produces user-facing text.**

### L4 Therapist — the user-facing voice

Receives both raw `M|` memos (as `[ANALYST MEMO]` and `[SUPERVISOR MEMO]`).
Generates the actual therapeutic response in
English using the `R|` Result wire format (SystemPing checkpoint). Hard constraints
in the prompt: no echo, ask one open question, under 200 words.

### L6 Calibrator — editorial polish

Same content, better prose. Removes formulaic openings ("I understand that...", "It seems that..."). Cannot introduce new topics or change facts. On failure, the flow falls back to the L4 draft.

## Session phase machine

```
INIT      ⤳  msg ≥ 3   ⤳   EXPLORATION
EXPLORATION ⤳ msg ≥ 8   ⤳   DIGGING
DIGGING   ⤳  msg ≥ 16   ⤳   WORKING
WORKING   ⤳  msg ≥ 24   ⤳   CLOSING
```

Forward-only. A phase transition triggers the L5 memory summarizer.

## 10 response strategies

`ResponseStrategySelector.Select(phase, severity, rupture)` returns one of:

| Strategy | When |
|----------|------|
| `Intake` | INIT + low/medium severity |
| `Mapping` | EXPLORATION + low/medium |
| `MappingWithNaming` | EXPLORATION + high (no crisis) |
| `Deepening` | DIGGING + low |
| `DeepeningWithMech` | DIGGING + moderate |
| `Intervention` | WORKING + low |
| `StabilizingIntervention` | WORKING + moderate |
| `Stabilizing` | any phase + high |
| `Closure` | CLOSING |
| `Repair` | rupture detected (any phase) |

## State storage

`InMemoryTherapyStateRepository` keyed by session ID (stable hash of the first user message). State carries: phase, message count, active topics, last 6-40 history messages, optional session summary written by L5.

Restart-volatile by design. Production deployments swap in SQLite/Postgres by implementing `ITherapyConversationStateRepository`.

## OpenAI compatibility

```
POST /v1/chat/completions     JSON or SSE (when stream:true)
GET  /v1/models               LibreChat-compatible probe
GET  /v1/trace/{sessionId}    debug — all layer events
DELETE /v1/trace/{sessionId}  manual trace eviction
```

LibreChat sends `stream:true` by default — the SSE response emits `delta.role` and `delta.content` chunks followed by `[DONE]`.

Custom response headers:
- `X-HT-Flow: hybrid-therapist`
- `X-HT-Fallback: true|false`

Custom response metadata fields:
- `session_id`, `trace_url`
- `phase`, `strategy`, `severity`, `message_count`, `topics`
- `analyst_severity`, `supervisor_approach`
- `rupture_detected`, `rupture_reason`, `thematic_alignment`
- `crisis_detected`, `fallback`
