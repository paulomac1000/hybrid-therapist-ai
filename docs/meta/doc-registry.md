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

# Rejestr dokumentów

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


## EDGE_CASES

- CASE: Nowy dokument dodany bez aktualizacji rejestru → OCZEKIWANE: CI flaguje niezgodność między systemem plików a rejestrem.

## NON_GOALS

- Nie wymienia plików CI/CD.
- Nie wymienia plików z kodem źródłowym ani pakietów NuGet.
