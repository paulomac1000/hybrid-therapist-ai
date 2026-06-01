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
last_verified: 2026-05-23
owners: ["hybrid-therapist"]
---

# Glosariusz

## DEFINITIONS

- **Socrates Pipeline** — 17-warstwowy, wieloagentowy pipeline terapeutyczny orkiestrujący 6 lokalnych LLM przez Ollamę.
- **M| Memo** — Performatyw wire-format (output `Memobuilder`) do komunikacji klinicznej między warstwami L2 Analityk i L3 Supervisor. Format: `M|L=N|key=value|...`.
- **R| Result** — Performatyw wire-format dla outputu LLM niosący wynik konfidencji i tekst odpowiedzi. Format: `R|C=0.95|V=odpowiedź`.
- **Implicit Priming** — Uczenie modelu formatu wire przez przykłady w historii konwersacji (checkpointy) zamiast przez jawne instrukcje w system prompcie.
- **Resilience Ladder (Drabina odporności)** — 5-stopniowy pipeline degradacji (strict → lenient → markdown_strip → semantic → unstructured) do parsowania outputu modeli.
- **AgentClass** — Klasyfikacja behawioralna LLM: Native (frontier), Assisted (mały lokalny), Reasoning (CoT), External (MCP/REST).
- **CompressionTier** — Poziom szczegółowości kluczy wire-format: Debug (pełne nazwy), Balanced (krótkie aliasy), Compact (pojedyncze litery).
- **Memobuilder** — Fluent builder do konstruowania wiadomości `M|` z kluczami adaptującymi się do poziomu kompresji. Rdzeń: `HandCodec.Parser.Memobuilder`.
- **CrisisGate** — Warstwa -1 Socrates — regex-owe twarde zatrzymanie na słowach kluczowych samobójstwa/samookaleczenia. Uruchamiana przed jakimkolwiek wywołaniem LLM.
- **PrivacySanitizer** — Warstwa 0 Socrates — redakcja PII (email, telefon, PESEL, imiona i nazwiska) przed jakimkolwiek wywołaniem LLM.
- **HandCodec** — Biblioteka .NET NuGet dostarczająca kodek wire-format (enkoder, parser, pipeline odporności, Memobuilder).
- **HandRuntime** — Biblioteka .NET NuGet dostarczająca orkiestrację (ConversationBuilder, ResponseDecoder, WireConvention, CheckpointLibrary).
- **ThematicAlignment** — Warstwa 8, strażnik anty-halucynacyjny — weryfikuje, czy tematy zgłoszone przez analityka faktycznie występują w inpucie użytkownika.
- **ResponseStrategy** — Jedna z 10 strategii terapeutycznych wybierana przez fazę × nasilenie × detekcję zerwania.
- **TokenSavingsTracker** — Narzędzie mierzące redukcję rozmiaru promptu dzięki kompresji wire-format vs. rozwinięcie plaintext.
