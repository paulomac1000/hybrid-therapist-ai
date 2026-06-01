---
description: Security invariants crisis detection and PII redaction for the hybrid-therapist pipeline
doc_id: ref.security
type: ref
status: active
rigor_tier: L2
ttl_days: 180
stability: stable
ai_scope: editable
upstream:
  - sys.socrates-pipeline
  - ref.glossary
tags: ["security", "crisis", "privacy", "pii", "safety"]
last_verified: 2026-05-23
owners: ["hybrid-therapist"]
---

# Hybrid Therapist — Bezpieczeństwo

## Niezmienniki

1. **`CrisisGate.Check()` runs before any LLM call.** Layer -1 in the pipeline. No exception, no skip flag.
2. **`PrivacySanitizer.Sanitize()` runs before any LLM call.** Layer 0. PII never reaches Ollama or OpenRouter.
3. **No raw user text reaches L4 Therapist.** Sanitised, then translated to EN, then passed to the model.
4. **Crisis hard-stops return canned helpline text, never LLM-generated content.** The model is never asked to handle a suicide ideation message.

These invariants are enforced by the `TherapistFlow` layer order, not by runtime flags. They cannot be turned off via configuration.

## CrisisGate

Four regex tiers, ordered by severity:

| Tier | Pattern (Polish + English) | Action |
|------|---------------------------|--------|
| **HardStop** | `samobój`, `zabiję`, `umrzeć`, `skończyć z sob`, `suicide`, `kill myself`, `end my life`, `want to die` | Return helpline (`116 123`), block flow |
| **High severity** | `nie daję rady`, `nie wytrzymam`, `już nie mogę`, `załamałem się`, `przytłoczony`, `nie widzę wyjścia` | Continue flow, append disclaimer at L9 |
| **Medium severity** | `nie śpię`, `bezsenność`, `kołatanie serca`, `ciągle zmęczony` | Continue flow, skip disclaimer (therapeutically correct) |
| **Safe** | none of the above | Continue flow |

Patterns use compile-time `[GeneratedRegex]` with a 200 ms timeout to defeat ReDoS. `RegexMatchTimeoutException` is caught and treated as "safe" — fail-open is the right move because the model is still bounded by other gates.

**Wzorce po polsku zostają po polsku.** Są interfejsem użytkownika — tłumaczenie złamałoby detekcję.

## PrivacySanitizer

Replaces matching patterns with role-appropriate placeholders before the text reaches any LLM:

| Pattern | Replacement |
|---------|-------------|
| Email (`user@example.com`) | `[EMAIL]` |
| Polish phone (`+48 123 456 789`, `123-456-789`) | `[TELEFON]` |
| PESEL (11 digits) | `[PESEL]` |
| Full name (heuristic) | `[OSOBA]` |

The sanitiser is intentionally aggressive — false positives in personal pronouns or place names are acceptable because the cost of a leaked PESEL is far higher than the cost of an over-redacted message.

## Testing requirements

Per the security rules: **every gate must have both positive and negative tests.** A test file with only passing cases is a violation.

Current coverage (`tests/HybridTherapist.Tests/CrisisGateTests.cs`):

- Positive: each hard-stop and severity phrase fires the expected level
- Negative: ambiguous phrases ("chcę skończyć z tym projektem", "want to die laughing") do not fire
- Boundary: empty input, whitespace, very long input

## Czego ten kod NIE zabezpiecza

- **Transport security.** Run behind TLS-terminating reverse proxy in production.
- **Authentication.** No API key check on `/v1/chat/completions`. Add via ASP.NET Core middleware if needed.
- **Rate limiting.** No per-IP or per-session rate limit. Use a fronting proxy or `Microsoft.AspNetCore.RateLimiting`.
- **Audit log persistence.** `_logger.LogInformation` writes to stdout. Pipe to a real audit sink in production.
- **VRAM enforcement.** This repo is demo-grade. Production deployments should add a VRAM guard around model swaps.

## Behaviour matrix — crisis vs phase

| User input | Phase | CrisisGate result | Disclaimer at L9 |
|-----------|-------|-------------------|------------------|
| "nie mogę zasnąć" | INIT | medium escalation | skipped (insomnia ≠ crisis) |
| "nie mogę zasnąć" | DIGGING | medium escalation | skipped (medium is too low even mid-session) |
| "nie daję rady" | INIT | high escalation | skipped (INIT phase — don't open with helpline) |
| "nie daję rady" | DIGGING | high escalation | appended ("Telefon Zaufania: 116 123") |
| "chcę skończyć z sobą" | any | hard-stop | helpline returned as the full response — L1-L7 skipped |

## Failure modes

| Failure | Consequence | Mitigation |
|---------|-------------|------------|
| Regex timeout | Treated as safe — flow continues | 200 ms bound; fail-open is acceptable |
| Sanitiser bug | PII leaks to Ollama | Cover with property-based tests; review on every change |
| Wrong helpline number | User can't reach help | Hardcoded `116 123` — Polish Telefon Zaufania; verify with national directory annually |
| Hard-stop bypass | LLM sees crisis input | Pipeline order makes this structurally impossible. Test enforces it |
