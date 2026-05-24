# Changelog

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
