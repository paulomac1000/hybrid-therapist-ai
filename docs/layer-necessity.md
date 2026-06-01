---
description: Necessity-proving test contracts for each Socrates pipeline layer
doc_id: ref.layer-necessity
type: ref
status: active
rigor_tier: L2
ttl_days: 180
stability: stable
ai_scope: editable
upstream:
  - sys.socrates-pipeline
  - ref.glossary
tags: ["testing", "necessity", "layers", "contracts"]
last_verified: 2026-05-23
owners: ["hybrid-therapist"]
---

# Layer Necessity — Why Each Layer Earns Its Place

Every layer in the Socrates pipeline has a measurable contribution to the final output. The `LayerNecessityTests` suite pins these contracts: each test fails if the corresponding layer's wiring is removed.

## Map

| Layer | Test | What the test guards |
|-------|------|----------------------|
| **CrisisGate** | `NECESSITY_CrisisGate_BlocksSuicideIdeation_BeforeAnyLlmCall` | Without the gate, suicide ideation reaches the LLM, which is unsafe |
| **CrisisGate (neg)** | `NECESSITY_CrisisGate_PassesNormalInsomnia` | Without false-positive guarding, every "nie mogę zasnąć" would crisis-stop |
| **TopicRegistry** | `NECESSITY_TopicRegistry_FeedsAnalystPrompt` | Without feeding topics to the analyst, the registry is dead weight; pins `AnalystLayer.RunAsync` API |
| **RuptureDetector** | `NECESSITY_Rupture_ForcesRepairStrategy_OverridingPhaseSelection` | Without rupture handling, "źle mnie rozumiesz" gets routed to `Deepening` instead of `Repair` |
| **ThematicAlignment (pos)** | `NECESSITY_ThematicAlignment_RejectsAnalystFabricatingBetrayal_FromSleepInput` | Without this guard, the analyst can hallucinate "betrayal" from sleep input and the chain runs on the fabrication |
| **ThematicAlignment (neg)** | `NECESSITY_ThematicAlignment_AllowsSupportedThemes` | Without negative test, the guard may over-block legitimate references |
| **M\| Memo (L2→L3→L4)** | `NECESSITY_AnalystMemo_IsParseableByDownstreamLayer` | Without parseable wire format, downstrpam layers lose clinical signal — HandParser round-trip integrity must hold for raw M\| wire |
| **SessionPhase guidance** | `NECESSITY_SessionPhase_ProducesDistinctGuidancePerPhase` | If GetPhaseSystemPrompt collapsed to one string for all phases, the phase machine would have no behavioural effect |
| **L5 MemoryService** | `NECESSITY_MemoryService_CompactsHistoryWhenTriggered` | Without compaction, L4 prompt grows linearly with session and exceeds context window |
| **QualityValidator (EN)** | `NECESSITY_QualityValidator_CatchesPromptLeakage_BeforeUserSeesIt` | Without EN-side QA, "confidence_decimal is high" leaks to the user |
| **QualityValidator (PL)** | `NECESSITY_PolishQualityCheck_RejectsEnglishOutput` | Without PL-side QA, English passes through pretending to be Polish |
| **Disclaimer (phase-aware)** | `NECESSITY_PhaseAwareDisclaimer_SkipsHelplineOnInitForInsomnia` | Without phase-awareness, every "nie mogę zasnąć" opens with crisis helpline → bad rapport |

## How to read these tests

Each test is named `NECESSITY_<component>_<observable behaviour>`. The "observable behaviour" phrase is the contract — the externally visible thing the layer must do.

If you delete or bypass a layer and these tests still pass, the test is wrong (too weak). Strengthen it. The point is to make architectural changes machine-checkable: refactor with confidence, lose nothing silently.

## Adding a new layer

When you add a layer, the necessity invariant requires:

1. A unit test in `LayerNecessityTests` proving what the layer contributes.
2. A row in this table.
3. A row in [architecture.md](architecture.md) "Data flow between layers".
4. If the layer produces wire format, a HandParser round-trip test verifying raw M\| parseability.

If you can't write a test that fails when the layer is removed, the layer likely doesn't earn its place.
