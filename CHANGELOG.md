# Changelog

## v0.4.0 (2026-06-04)

### SonarQube quality cleanup
- Fixed `app.Run()` → `await app.RunAsync()` in Program.cs (S6966)
- Suppressed S1118 on `Program` class — `WebApplicationFactory<T>` requires non-static partial class
- Merged nested `if` in `MemorySummaryParser.ParseTopicLine` (S1066)
- Fixed 5× CS8602/CS8600/CS8604 null dereference warnings in `HandCheckpointCountBenchmarkTests`
- Replaced 10 repeated string literals with named constants (S1192): ChatEndpoints, TherapistFlow, HandCheckpointLibrary, SupervisorLayer, StackYamlOptionsBinder, SessionPhase
- Fixed 2× S6444 regex timeout: `MemorySummaryParser` (→ `[GeneratedRegex]` with 200ms timeout), `SupervisorLayer.ExtractApproachFromPlaintext` (→ `[GeneratedRegex]` with 200ms timeout)
- Suppressed 3× S3358 nested ternary in `ResponseStrategySelector` — intentional phase×severity map
- Refactored `TokenSavingsTracker.ExpandMemoToPlaintext` — 9 repetitive if-blocks replaced with table-driven `_fieldDefs` array. Cognitive complexity: 33 → estimated ~5
- Extracted `ParseSupervisorResponse()` from `SupervisorLayer.RunAsync` — cognitive complexity: 18 → <15
- Suppressed S3776 with rationale comments for: `TherapistFlow.ExecuteAsync` (17-layer orchestration), `ChatEndpoints.HandleCompletions` (HTTP request/streaming handler), `MemorySummaryParser.ParseTopicLine` (string parser)
- Suppressed S107 with rationale comments for: `TherapistFlow` constructor (DI), `RunL4TherapistAsync` (pipeline context)

### HandCodec upgrade: 0.2.1 → 0.3.0
- Resilience ladder expanded from 5 to 6 levels: Level 5 = JSON extraction (new, opt-in), Level 6 = passthrough/fallback (was Level 5)
- Updated `AnalystLayer` and `SupervisorLayer` fallback guards from `parsed.Level >= 5` to `parsed.Level >= 6`
- Updated fallback memo identifier strings from `decoder_level5_fallback` to `decoder_level6_passthrough`
- Updated 2 unit tests and 1 integration test for new level numbering
- Character escaping (`|`, `=`, `\\`) in encoder/parser — fully backwards-compatible
- Enhanced markdown list parsing and blockquote stripping — fully backwards-compatible
- JSON Resilience Stage (Level 5) — opt-in via `HandResilientOptions.EnableJsonExtraction`. Default off, no behavioral change
- `HandResilientOptions.AllEnabled` now includes `EnableJsonExtraction: true`
- Local packages updated: `local-packages/HandCodec.0.3.0.nupkg`, `local-packages/HandRuntime.0.3.0.nupkg`
- Removed stale `HandCodec.0.2.1-local` and `HandRuntime.0.2.1-local` packages
- `.csproj` references updated from `0.2.1-local` to `0.3.0`

### Dead code removal
- Removed `HandWireConvention.cs` — application facade with zero production callers (4 tests deleted)
- Removed `ClinicalReport`, `ClinicalSeverity`, and `TherapeuticPlan` records — were never populated; `AnalystResult.Report` was always `null`
- Removed 5 unused extension methods from `TherapistMemoBuilderExtensions`: `RiskIndicators`, `CognitivePatterns`, `EvidenceQuotes`, `SessionGoal`, `CrisisFlag`
- Removed empty stub `HandCheckpoint.cs` — types already re-exported via `RuntimeAliases.cs` global using aliases

### Benchmark infrastructure quality
- `HandConversationBuilder.Build()` — added `ArgumentNullException.ThrowIfNull` guards on persona, checkpoint, userText (6 guards total)
- `TokenSavingsTracker.StrictCodecG` — migrated from `[ThreadStatic]` to `AsyncLocal<bool>` to prevent state leakage across async tests

### Token savings assertions
- Compact: `savings.SavingsPercent.Should().BeGreaterThan(15.0)`
- Semantic: `savings.SavingsPercent.Should().BeGreaterThan(0.0)`
- JSON: `savings.SavingsPercent.Should().BeGreaterThan(0.0)`
- Plaintext: `savings.SavingsPercent.Should().BeLessThan(0.0)` (negative = larger than compact baseline)

### Benchmark reports — critical fix
- **Root cause**: `parse_token_savings()` in `run-hand-benchmark.sh` scraped stdout where xUnit suppresses `ITestOutputHelper` output for passing tests, and the regex also captured `TherapistFlow` log lines with a different savings formula
- **Fix**: `parse_token_savings()` now reads from TRX files using the unique `BENCHMARK_TOKEN_SAVINGS=` marker; C# tests emit this marker alongside human-readable `Token save:` lines
- **Result**: Semantic, JSON, and Plaintext `-latest.md`/`-latest.json` files now report **correct** token savings (were 0.0% or -616.7%; now match TRX data)
- **Regenerated all `artifacts/benchmarks/*-latest.*`** reports

### Documentation
- `docs/architecture.md` — removed references to deleted `HandWireConvention`
- Added `AGENTS.md` — build/test conventions, architecture overview, key invariants for AI agents working on this repo

### New benchmark variants (added in this release cycle)
- **Semantic** (`hand-semantic-*` cassettes, `HandSemanticBenchmarkTests`, `HandSemanticBenchmarkValidator`) — 3 scenarios, 37.2% avg savings
- **JSON** (`json-*` cassettes, `HandJsonBenchmarkTests`, `HandJsonBenchmarkValidator`) — 3 scenarios, 7.1% avg savings
- **Plaintext** (`plaintext-*` cassettes, `HandPlaintextBenchmarkTests`, `HandPlaintextBenchmarkValidator`) — 3 scenarios, -147.6% avg (baseline overhead)
- **Checkpoint count experiment** (`HandCheckpointCountBenchmarkTests`) — 0/1/3/5 checkpoints, confirms 3 is production default
- **Long session drift** (`HandLongSessionDriftBenchmarkTests`) — multi-turn format adherence

### Benchmark results (cassette mode, 2026-06-04)
| Variant | Passed | Failed | Avg Token Savings |
|---------|--------|--------|-------------------|
| Compact | 19 | 0 | 34.4% |
| Semantic | 6 | 0 | 37.2% |
| Plaintext | 6 | 0 | -147.6% |
| JSON | 6 | 0 | 7.1% |
| Checkpoints | 4 | 0 | — |

### Tests
- 280 unit tests (+3 from v0.3.0; -4 HandWireConvention removed)
- 37 cassette integration tests across 4 variants + negative + checkpoints
- All passing, 0 skipped, 0 warnings

---

## v0.3.0 (2026-06-01)

### Codec G — random keys experiment
- All inter-agent wire keys replaced with arbitrary identifiers: `em→e7`, `sv→s9`, `ri→x4`, `cp→y1`, `ev→q3`, `ap→p3`, `tk→t5`, `kq→k2`, `rn→r8`
- L2 Analyst and L3 Supervisor emit Codec G via checkpoint-based implicit priming
- L4 Therapist receives raw `M|` memo lines without any key legend in system prompt (Pure Implicit mode)
- `STRICT_CODEC_G` flag in `TokenSavingsTracker` — blocks verbose-key fallback for research benchmarks

### Benchmark hardening
- 11 scenario-specific cassettes (`hand-*.json`) with per-scenario `user_input_pl`
- `HandBenchmarkTests`: L2/L3 Codec G validated from trace, token economy computed from memo wire (not final response)
- `HandBenchmarkNegativeTests`: old semantic keys (`em`, `sv`, `ap`) cause benchmark failure; English output fails Polish diacritic check
- `HandBenchmarkValidator`: strict prompt purity, fallback hard-fail, required_topic/phrase hard assertions
- `run-hand-benchmark.sh`: `--cassette`/`--live`/`--all` modes, measured (not hardcoded) token savings
- Token economy measured per-benchmark from `TokenSavingsTracker.ExpandMemoToPlaintext()` in strict mode

### Documentation
- `docs/benchmarks/hand-codec-g.md`: full benchmark report (hypothesis, setup, scenarios, results, limitations)
- `docs/benchmarks/benchmark-matrix.md`: planned comparison matrix (JSON vs H.A.N.D., checkpoints count, model swap)
- All active docs updated to Codec G keys; stale "dictionary keys in the system prompt" references removed
- `afds_config.yaml`: exempted `docs/benchmarks/` from AFDS validation
- Cassettes `README.md`: new benchmark scenario table

### Tests
- 277 unit tests (+5 new: prompt purity, topic registry, therapist layer service)
- 19 benchmark/integration tests (+3 new: negative tests, validator)

---

## v0.2.0 (2026-05-24)

### Therapy logic overhaul (#7)
The Socrates pipeline was technically correct but therapeutically passive — spending 3-4 messages gathering data before offering any help. This release makes the pipeline genuinely helpful from message 2 onward.

**Phase Machine:**
- Severity-aware phase transitions: INIT→EXPLORATION at 1 message for moderate+ severity (was 3)
- EXPLORATION→DIGGING at 4 messages for moderate+ (was 8)
- INIT prompt now allows gentle suggestions when user explicitly asks for help

**Severity Detection — 6 new categories:**
- Anhedonia/depression (high), social withdrawal (moderate), panic/anxiety (high)
- Anger/irritability (moderate), cognitive complaints (moderate), insomnia extended (moderate)

**Supervisor — concrete techniques:**
- 8+ specific therapeutic techniques: behavioral_activation, sleep_hygiene, boundary_setting, grounding, cognitive_restructuring, breathing, activity_scheduling
- Fallback approach changed from generic "reflective_listening" to "behavioral_activation"

**Therapist — formulaic opening guard:**
- Forbidden openings list in L4 Therapist and L6 Calibrator prompts
- QualityValidator.therapeutic quality detector now enforces (blocks) bad responses
- English + Polish detectors for formulaic openings and advice presence

**Memory — emotional trend tracking:**
- New EmotionalTrend field: improving | stable | worsening based on emotional arc comparison

**Additional improvements:**
- RuptureDetector: expanded patterns for repeated frustration, being ignored
- ThematicAlignment: 3 new sensitive categories (self_harm, eating_disorder, psychosis)
- ResponseStrategy: INIT + moderate severity → Mapping (was Intake)

### CI/CD (#4, #6)
- GitHub Packages NuGet feed replaces local-packages
- Docker build downloads .nupkg from public GitHub Release (no auth needed)
- Semgrep security scanning, docs-validation, auto-tag, dependabot (docker + nuget + github-actions)
- NuGet cache, coverage reporting, Docker smoke test in CI

### Documentation
- AFDS-compliant YAML frontmatter on all docs (10/10 passing)
- Docs meta files: glossary, doc-registry, health-report
- CHANGELOG exemption from AFDS validation

### Tests
- 265 unit tests (+40 new in this release)
- Integration test for full therapy pipeline with severity escalation

---

## v0.1.0 (2026-05-23)

### Initial release
- 17-layer Socrates multi-agent therapy pipeline (6 local LLMs via Ollama)
- HandCodec v0.2.0 + HandRuntime v0.2.0 — wire-format inter-layer communication
- Performatives: Result (L1/L4/L7), Memo (L2/L3) via Implicit Priming
- 5-level Resilience Ladder (strict → lenient → markdown → semantic → unstructured)
- MemoBuilder with 11 domain-specific extension methods
- TokenSavingsTracker — measured wire-format compression savings
- AgentClass.Assisted default, configurable CompressionTier
- CrisisGate (Polish suicide detection) + PrivacySanitizer (PII redaction)
- OpenAI-compatible API (/v1/chat/completions, /v1/models, /v1/trace)
- Documentation: architecture, pipeline guide, API reference, security model, layer necessity
- CI/CD: lint → test → docker-smoke pipeline
- Docker Compose stack (Ollama + therapist + model-loader + LibreChat)
- 196 unit tests
