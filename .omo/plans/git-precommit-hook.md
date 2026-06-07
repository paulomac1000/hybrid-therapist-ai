# Git Precommit Hook — Code Quality Gate

## TL;DR

> **Problem**: Wypchnięto kod który nie przechodzi testów. Brak lokalnej bramki jakości przed commitem.
>
> **Fix**: Git precommit hook: restore → format → build → unit tests → semgrep → AFDS docs validation. Wszystko MUSI przejść, inaczej commit blokowany.
>
> **Dodatkowo**: AGENTS.md — instrukcja dla AI: naprawiaj błędy, nie ukrywaj ich.

---

## Diagnosis

### Co poszło nie tak
Wypchnięto `LiveOllama_MultiTurn_MemoryContext` który failował na `KeyNotFoundException` przy `meta.GetProperty("message_count")` w odpowiedzi fallback. Zamiast naprawić przyczynę (fallback nie zawiera `message_count`), test został rozluźniony do logowania.

**Root cause**: Brak lokalnej bramki jakości. Agent mógł commitować bez przejścia testów.

### Co hook ma wymuszać
| Krok | Komenda | Blokuje commit na fail? |
|------|---------|------------------------|
| restore | `dotnet restore` | tak |
| format | `dotnet format --verify-no-changes` | tak |
| build | `dotnet build -c Release --nologo -v q` | tak |
| unit tests | `dotnet test tests/HybridTherapist.Tests -c Release --nologo` | tak |
| semgrep | `semgrep --config auto --error` | tak |
| AFDS docs | `python3 docs_validate.py --config afds_config.yaml ./` | tak |

---

## TODOs

- [x] 1. Create `.githooks/pre-commit` bash script
- [x] 2. Configure git to use `.githooks/` directory
- [x] 3. Update AGENTS.md — "naprawiaj błędy, nie ukrywaj"
- [x] 4. Run the hook against current code to verify it works

  **Agent**: `quick`

---

## Commit Strategy

| Commit | Files |
|--------|-------|
| `build: add git precommit hook (restore + format + build + test + semgrep + afds)` | .githooks/pre-commit, AGENTS.md |
