# Multi-Message E2E Conversation Tests

## TL;DR

> **Quick Summary**: Testy E2E dla wielowiadomościowych konwersacji — phase transition, rupture detection, concrete technique requests, memory accumulation. Uzupełnienie 6 istniejących testów single-message.
>
> **Scenariusze**: 4 multi-message flows (3+ wiadomości każdy)

---

## Context

### Istniejące testy (6 single-message)
Insomnia, Greeting, Depression, Anxiety, Positive, Crisis — każdy z 1 wiadomością.

### Co testują multi-message
- **Phase transitions**: INIT → EXPLORATION (msg 3) → DIGGING (msg 8)
- **Rupture detection**: użytkownik mówi "to mi nie pomaga" → strategy=Repair
- **Concrete requests**: "co konkretnie mam zrobić?" → brak fallbacku, konkretna odpowiedź
- **Session continuity**: ta sama sesja, rosnący message_count
- **Memory**: tematy z wcześniejszych wiadomości obecne w analizie

---

## TODOs

### Wave 1 — Multi-message test helper

- [x] 1. Add `ExecuteMultiMessage(params string[] userMessages)` helper
- [x] 2. `LiveOllama_MultiTurn_PhaseTransition`
- [x] 3. `LiveOllama_MultiTurn_RuptureDetection`
- [x] 4. `LiveOllama_MultiTurn_ConcreteTechniqueRequest`
- [x] 5. `LiveOllama_MultiTurn_MemoryContext`
- [x] 6. Run all 10 E2E tests (6 single + 4 multi), capture results

  **Agent**: `quick`

---

## Commit Strategy

| Commit | Files |
|--------|-------|
| `test: add multi-message E2E scenarios (phase, rupture, concrete, memory)` | LiveOllamaE2ETests.cs |
