---
description: Registry of all documentation files in the hybrid-therapist project
doc_id: ref.doc-registry
type: ref
status: active
rigor_tier: L2
ttl_days: 365
stability: stable
ai_scope: editable
source_of_truth: true
upstream:
  - ref.glossary
tags: ["registry", "index", "meta"]
last_verified: 2026-06-06
owners: ["hybrid-therapist"]
---

# Document Registry

## RULES

| doc_id | Path | Type | Status | Description |
|--------|------|------|--------|-------------|
| `sys.socrates-pipeline` | `docs/architecture.md` | system | active | Architecture of the 19-layer Socrates pipeline with HandCodec wire format |
| `guide.socrates-pipeline` | `docs/socrates-pipeline.md` | guide | active | Walkthrough: wire format, Implicit Priming, resilience ladder |
| `ref.api-reference` | `docs/api.md` | ref | active | OpenAI-compatible HTTP API reference |
| `ref.layer-necessity` | `docs/layer-necessity.md` | ref | active | Necessity-proving test contracts per pipeline layer |
| `ref.security` | `docs/security.md` | ref | active | CrisisGate, PrivacySanitizer, security invariants |
| `ref.glossary` | `docs/meta/glossary.md` | ref | active | Domain term glossary |
| `ref.doc-registry` | `docs/meta/doc-registry.md` | ref | active | This file — document registry |
| `ref.health-report` | `docs/meta/health-report.md` | ref | active | CI-generated documentation health metrics |
| `bench.hand-compact` | `docs/benchmarks/hand-compact.md` | benchmark | active | H.A.N.D. Compact implicit priming experiment — benchmark report |
| `bench.hand-semantic` | `docs/benchmarks/hand-semantic.md` | benchmark | active | H.A.N.D. Semantic keys experiment — benchmark report |
| `bench.plaintext` | `docs/benchmarks/plaintext.md` | benchmark | active | Plaintext memo token overhead — benchmark report |
| `bench.json` | `docs/benchmarks/json.md` | benchmark | active | JSON memo structured serialization — benchmark report |
| `bench.checkpoints` | `docs/benchmarks/checkpoints.md` | benchmark | active | Checkpoint count strength experiment — benchmark report |
| `bench.benchmark-matrix` | `docs/benchmarks/benchmark-matrix.md` | benchmark | active | Planned comparison matrix for H.A.N.D. benchmark variants |
| `ref.agents` | `AGENTS.md` | ref | active | Build/test instructions and key invariants for AI agents |
| `ref.changelog` | `CHANGELOG.md` | ref | active | Release history and breaking changes |
| `ref.readme` | `README.md` | ref | active | Project overview, quick start, and architecture summary |
| `ref.cassettes` | `tests/HybridTherapist.Integration/Cassettes/README.md` | ref | active | VCR-style cassettes for offline pipeline testing |


## EDGE_CASES

- CASE: A new document is added without a registry update → EXPECTED: CI flags a mismatch between the filesystem and the registry.

## NON_GOALS

- Does not list CI/CD workflow files.
- Does not list source code files or NuGet packages.
