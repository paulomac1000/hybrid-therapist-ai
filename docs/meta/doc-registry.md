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
last_verified: 2026-05-23
owners: ["hybrid-therapist"]
---

# Document Registry

## PURPOSE

Single source of truth listing every documentation file in this project, its type, status, and dependencies.

## SCOPE

- INCLUDED: All AFDS-governed documentation files under `docs/`.
- EXCLUDED: README.md (human-first, exempt from AFDS), source code, CI/CD configs.

## RULES

| doc_id | Path | Type | Status | Description |
|--------|------|------|--------|-------------|
| `sys.socrates-pipeline` | `docs/architecture.md` | system | active | Architecture of the 17-layer Socrates pipeline with HandCodec wire format |
| `guide.socrates-pipeline` | `docs/socrates-pipeline.md` | guide | active | Walkthrough: wire format, Implicit Priming, resilience ladder |
| `ref.api-reference` | `docs/api.md` | ref | active | OpenAI-compatible HTTP API reference |
| `ref.layer-necessity` | `docs/layer-necessity.md` | ref | active | Necessity-proving test contracts per pipeline layer |
| `ref.security` | `docs/security.md` | ref | active | CrisisGate, PrivacySanitizer, security invariants |
| `ref.glossary` | `docs/meta/glossary.md` | ref | active | Domain term glossary |
| `ref.doc-registry` | `docs/meta/doc-registry.md` | ref | active | This file — document registry |
| `ref.health-report` | `docs/meta/health-report.md` | ref | active | CI-generated documentation health metrics |

## DEFINITIONS

N/A

## INTERFACES

- INPUT: Manual updates on document create/move/deprecate.
- OUTPUT: Single index for AI retrieval and CI validation.

## STATE

- Assumptions: Registry is manually maintained. CI may validate consistency.
- Known Limitations: Does not track external references (HandCodec docs, GitHub READMEs).

## EDGE_CASES

- CASE: New document added without registry update → EXPECTED: CI flags mismatch between filesystem and registry.

## EXAMPLES

N/A

## NON_GOALS

- Does not list CI/CD workflow files.
- Does not list source code files or NuGet packages.
