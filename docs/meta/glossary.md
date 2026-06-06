---
description: Glossary of domain terms for the hybrid-therapist Socrates pipeline and HandCodec integration
doc_id: ref.glossary
type: ref
status: active
rigor_tier: L2
ttl_days: 365
stability: stable
ai_scope: editable
source_of_truth: true
upstream: []
tags: ["glossary", "terminology"]
last_verified: 2026-06-01
owners: ["hybrid-therapist"]
---

# Glossary

## DEFINITIONS

- **Socrates Pipeline** — The 17-layer multi-agent therapy pipeline orchestrating 6 local LLMs via Ollama.
- **M| Memo** — Wire-format performative (`MemoBuilder` output) for inter-layer clinical communication between L2 Analyst and L3 Supervisor. Format: `M|L=N|key=value|...`.
- **R| Result** — Wire-format performative for LLM output carrying confidence and answer text. Format: `R|C=0.95|V=answer`.
- **Implicit Priming** — Teaching a model the wire format through conversation-history examples (checkpoints) rather than explicit system-prompt instructions.
- **Resilience Ladder** — 6-level degradation pipeline (strict → lenient → markdown_strip → semantic → json_extraction → unstructured) for parsing model outputs.
- **AgentClass** — Behavioural classification of an LLM: Native (frontier), Assisted (small local), Reasoning (CoT), External (MCP/REST).
- **CompressionTier** — Verbosity level for wire-format keys: Debug (full names), Balanced (short aliases), Compact (single letters).
- **MemoBuilder** — Fluent builder for constructing `M|` wire messages with tier-adaptive key names. Core: `HandCodec.Parser.MemoBuilder`.
- **CrisisGate** — Layer -1 of the Socrates pipeline — regex-based hard-stop on suicide/self-harm keywords. Runs before any LLM call.
- **PrivacySanitizer** — Layer 0 of the Socrates pipeline — PII redaction (email, phone, PESEL, names) before any LLM call.
- **HandCodec** — .NET NuGet library providing the wire-format codec (encoder, parser, resilience pipeline, MemoBuilder).
- **HandRuntime** — .NET NuGet library providing orchestration (ConversationBuilder, ResponseDecoder, WireConvention, CheckpointLibrary).
- **ThematicAlignment** — Layer 8 anti-hallucination guard — verifies analyst-claimed themes appear in actual user input.
- **ResponseStrategy** — One of 10 therapeutic strategies selected by phase × severity × rupture detection.
- **TokenSavingsTracker** — Utility that measures prompt-size reduction from wire-format compression vs. plaintext expansion.
