# Changelog

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
