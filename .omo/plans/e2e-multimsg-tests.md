# E2E Multi-Message Conversation Tests

## TL;DR

> **Quick Summary**: Rozbudować testy E2E o wielowiadomościową konwersację — testowanie przejść faz, pamięci sesji, detekcji rupture, format adherence w multi-turn. Uzupełnienie obecnych 6 testów single-message.
>
> **Deliverables**: 3 scenariusze wielowiadomościowe, asercje na phase transition, memory, rupture detection.

---

## Context

### Obecny stan (6 testów single-message)
- Insomnia, Greeting, Crisis, Depression, Anxiety, Positive feedback
- Każdy test: jedna wiadomość → jedna odpowiedź
- Brak testowania: phase transitions, session memory, rupture detection, multi-turn format

### Co testuje multi-message
- **Phase transitions**: INIT → EXPLORATION (msg 3) → DIGGING (msg 8)
- **Memory/Summarization**: L5 MemoryService compaction przy phase change
- **Rupture detection**: "źle mnie rozumiesz" → Repair strategy
- **Session continuity**: ta sama sesja, rosnący kontekst
- **Format adherence**: czy L2/L3 dalej produkują poprawne M| memo w turze 2+

---

## TODOs

### Wave 1 — Multi-message test helper

- [ ] 1. Add `SendMessage(client, sessionId, content)` helper + `CreateSessionClient()`

  **File**: `tests/HybridTherapist.Integration/LiveOllamaE2ETests.cs`

  **What**: Metoda wysyłająca wiadomość wewnątrz istniejącej sesji. Potrzebujemy:
  - `CreateAppClient()` → tworzy `HttpClient` z nową fabryką
  - `SendMessage(client, sessionId, content)` → POST z session_id w metadata/user
  - Parsowanie odpowiedzi i zwracanie (content, metadata, sessionId)

  **Agent**: `quick`

### Wave 2 — 3 multi-message scenarios

- [ ] 2. `LiveOllama_MultiTurn_PhaseTransition_INIT_to_EXPLORATION`

  **Scenariusz**: 3 wiadomości w tej samej sesji
  ```
  msg1: "nie mogę zasnąć"       → phase: INIT
  msg2: "budzę się o 3 w nocy"   → phase: INIT
  msg3: "to trwa już miesiąc"    → phase: EXPLORATION (msg >= 3)
  ```
  **Asercje**:
  - wszystkie: fallback: false, Polish, no control tokens
  - msg3: phase = EXPLORATION
  - msg1-3: analyst_severity != "unknown" (w kontenerze działa, WAF ma bug)

  **Agent**: `quick`

- [ ] 3. `LiveOllama_MultiTurn_RuptureDetection_RepairStrategy`

  **Scenariusz**: 2 wiadomości — rupture w drugiej
  ```
  msg1: "nie mogę zasnąć"             → strategy: Intake/Mapping
  msg2: "źle mnie rozumiesz, nie o to chodzi" → rupture: true, strategy: Repair
  ```
  **Asercje**:
  - msg2: `rupture_detected: true`
  - msg2: `rupture_reason` != ""
  - msg2: `strategy` = "Repair"

  **Agent**: `quick`

- [ ] 4. `LiveOllama_MultiTurn_MemoryService_ContextPreserved`

  **Scenariusz**: 5 wiadomości — sprawdzenie czy kontekst jest pamiętany
  ```
  msg1: "mam problemy w pracy"
  msg2: "szef ciągle na mnie krzyczy"
  msg3: "przez to nie mogę spać"
  msg4: "czy powinienem zmienić pracę?"
  msg5: "co radzisz?"
  ```
  **Asercje**:
  - Wszystkie: fallback: false
  - msg5: response nawiązuje do poprzednich tematów (praca, szef, sen)
  - msg5: phase = EXPLORATION (lub dalej)
  - Sprawdzamy trace msg5: L2 memo powinien zawierać tematy z wcześniejszych wiadomości (topic registry)

  **Agent**: `quick`

### Wave 3 — Bulk run + report

- [ ] 5. Run all 9 E2E tests (6 single + 3 multi-message) — analyze results

  **Agent**: `quick`

---

## Commit Strategy

| Commit | Files |
|--------|-------|
| `test: add multi-message E2E scenarios (phase, rupture, memory)` | LiveOllamaE2ETests.cs |
