---
description: Build/test instructions and key invariants for AI agents working on hybrid-therapist
doc_id: ref.agents
type: ref
status: active
stability: stable
ai_scope: editable
tags: ["agents", "meta"]
last_verified: 2026-06-06
---

# AGENTS.md — instructions for AI agents working on this repo

## Build & test

```bash
# Build (check after every .cs change)
dotnet build HybridTherapist.sln -c Release --nologo -v q

# Local builds require .NET SDK 10.0+ (installed at ~/.dotnet/sdk/10.0.300)
export DOTNET_ROOT=$HOME/.dotnet
export PATH=$DOTNET_ROOT:$PATH

# Format check (MUST pass on CI — run before push)
dotnet format HybridTherapist.sln --verify-no-changes --no-restore

# Unit tests (fast, no Docker/Ollama)
dotnet test tests/HybridTherapist.Tests -c Release --no-build --nologo

# Integration tests — cassette mode (no Docker/Ollama)
./scripts/run-hand-benchmark.sh --cassette --all-variants --report

# Integration tests — live mode (requires Docker Compose + Ollama)
./scripts/run-hand-benchmark.sh --live

# Rebuild Docker image (after every .cs change that you want to test live)
./scripts/rebuild-therapist.sh
```

## Architecture

```
User (PL) → CrisisGate → PrivacySanitizer → L1 PL→EN → L2 Analyst → L3 Supervisor
→ L4 Therapist → L6 Calibrator → L7 EN→PL → User (PL response)
```

- **L2 Analyst** (`AnalystLayer.cs`) — emits `M|L=2|e7=...|s9=...` Codec G memo via Implicit Priming
- **L3 Supervisor** (`SupervisorLayer.cs`) — emits `M|L=3|p3=...|t5=...|k2=...`  memo
- **L4 Therapist** (`TherapistLayerService.cs` → `RunL4TherapistAsync`) — receives raw M| memos without legend
- **L6 Calibrator** — polishes style, removes formulaic openings
- **L1/L7 Translator** — Bielik PL↔EN translation
- Non-LLM layers: CrisisGate, PrivacySanitizer, StateLoader, TopicExtraction, PhaseMachine, RuptureDetector, ResponseStrategy, ThematicAlignment, QualityValidator, Disclaimer, Audit

## Key invariants (do not break)

1. **Models learn the wire format from checkpoints, never from system prompt** — the L4 prompt must be pure therapeutic instruction with no `M|` legend, no key explanation. Verified by `ValidateL4Input` in benchmark validators.
2. **`MemoPing` checkpoint is the format teacher** — L2/L3 see a `MemoPing` exchange before the user message. Do not add wire format instructions to any system prompt.
3. **`[SYSTEM_PROTOCOL_PING]` must be zero-therapy-content** — verified by `SystemPing_ContainsNoTherapeuticWords` test. Do not add clinical terms to protocol exchanges.
4. **Token savings are measured from memo wire, not final response** — benchmarks compare L2+L3 wire format vs expanded plaintext equivalent. Do not include L4 response text in these calculations.
5. **CrisisGate runs first** — before translation, before analysis. Hard-stop on suicidal ideation → helpline 116 123.
6. **HandCodec and HandRuntime are NuGet packages** — sources live in a separate repo (`paulomac1000/hand-codec`). Local packages are in `local-packages/`. Do not modify the hand-codec protocol without updating the NuGet package.

## Hand/ directory — facade pattern

Thin wrappers that delegate to HandRuntime types:
- `HandConversationBuilder.cs` → `HandRuntime.HandConversationBuilder`
- `HandResponseDecoder.cs` → `HandRuntime.HandResponseDecoder`
- `HandCheckpointLibrary.cs` → `HandRuntime.HandCheckpointLibrary`
- `TokenSavingsTracker.cs` — application-level token economy tracker (no runtime dependency)
- `CrisisKeywordDetector.cs` — Polish crisis keyword detection
- `TherapistMemoBuilderExtensions.cs` — domain-specific MemoBuilder extensions (6 methods)
- `RuntimeAliases.cs` — global using aliases re-exporting HandRuntime types as short names

## Benchmark variants

| Variant | Config key | Key format | Test class |
|---------|-----------|------------|------------|
| Compact | `Models:HandWireVariant=Compact` | `e7=`, `s9=`, `p3=`... | `HandBenchmarkTests` |
| Semantic | `Models:HandWireVariant=Semantic` | `em=`, `sv=`, `ap=`... | `HandSemanticBenchmarkTests` |
| Plaintext | `Models:HandWireVariant=Plaintext` | natural prose | `HandPlaintextBenchmarkTests` |
| JSON | `Models:HandWireVariant=Json` | `{"emotional_state":...}` | `HandJsonBenchmarkTests` |

## Conventions

- No comments on methods that are self-documenting by name
- Prefer native Home Assistant-style conditions over Jinja2 templates
- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Use `FluentAssertions` for test assertions
- Use `ArgumentNullException.ThrowIfNull()` in facade methods (not in decoder — it handles null/empty gracefully)
- Benchmark test output uses `BENCHMARK_TOKEN_SAVINGS=X.X` for machine-readable token savings (parsed by `run-hand-benchmark.sh` from TRX files)
- Dead code rule: any class with zero references in `src/` should be deleted (not commented out or left as stubs)

## Quality discipline (ALL agents)

- **NEVER** skip, relax, wrap-in-try-catch, or log-and-ignore a failing test. Fix the root cause or ask for help.
- **NEVER** commit code that doesn't build or pass unit tests. The pre-commit hook enforces this.
- **NEVER** use `- [~]` (blocked marker) to bypass a test that should be fixable.
- If a test is genuinely wrong (tests outdated behavior), update the test BEFORE updating production code, then run the full suite to confirm.
- If you cannot reproduce a failure locally, document exactly what steps you took and why the failure could not be reproduced — then escalate. Do not silently "relax" the assertion.
- The pre-commit hook at `.githooks/pre-commit` runs restore → format → build → unit tests → semgrep → AFDS validation. All must pass. No `--no-verify` unless explicitly authorized by a human.

## Documentation

- `docs/architecture.md` — architecture reference
- `docs/socrates-pipeline.md` — H.A.N.D. protocol walkthrough
- `docs/api.md` — API reference
- `docs/security.md` — CrisisGate, PrivacySanitizer
- `docs/layer-necessity.md` — per-layer necessity proofs
- `docs/benchmarks/` — per-variant benchmark reports
- `docs/meta/doc-registry.md` — document registry
- `CHANGELOG.md` — release history
- `artifacts/benchmarks/*-latest.md` — auto-generated per-run reports

## Precommit Hook

This repository uses a native git hook at `.githooks/pre-commit` that enforces code quality before every commit:

```bash
# The hook runs automatically on every commit (configured via core.hooksPath).
# It performs these steps in order:
#   1. dotnet restore                                    (L1 — blocking)
#   2. dotnet format HybridTherapist.sln --verify-no-changes --no-restore  (L2 — blocking on CI)
#   3. dotnet build Release                              (L1 — blocking)
#   4. dotnet test unit                                  (L1 — blocking)
#   5. semgrep                                           (L2 — non-blocking, tool may be missing)
#   6. AFDS docs validation                              (L3 — non-blocking)
#
# To bypass (only for urgent hotfixes):
git commit --no-verify
```

For hook design reference, see the [Precommit Hook Architect skill](/var/apps/ai-skills/skills/precommit-hook-architect/SKILL.md).
