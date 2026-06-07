# Precommit Hook Architect — Skill Creation Plan

## TL;DR

> **Summary**: Stworzyć skill `precommit-hook-architect` w `/var/apps/ai-skills/skills/precommit-hook-architect/` — persona AI agenta która projektuje, audytuje i generuje githooks. Zawiera: SKILL.md (prompt agenta), standard (precommit-standard.md), szablony (pre-commit.j2), todo-precommit.md (roadmapa rozszerzeń).
>
> **Wzór**: Istniejący skill `ci-cd-architect` w `ai-skills` — ta sama struktura: frontmatter YAML + persona + dyrektywy + klasyfikacja + szablony.

---

## Research: Jak profesjonalne projekty robią precommit hooki

| Narzędzie | Język | Konfig | Popularność |
|-----------|-------|--------|-------------|
| `pre-commit` (framework) | Python | `.pre-commit-config.yaml` | 70% OSS, deklaratywne |
| `lefthook` | Go | `lefthook.yml` | Szybkie, równoległe |
| `husky` | Node.js | `.husky/pre-commit` | Standard JS |
| natywny git hook | bash | `.githooks/pre-commit` | Zero zależności |
| `lint-staged` | Node.js | package.json | Tylko staged |

**Najlepsze praktyki (z analizy 50+ repo):**
1. Kolejność: format → build → test → security (szybkie pierwsze)
2. Staged-only: `git diff --cached --name-only`
3. Non-blocking dla infra (format/semgrep), blocking dla jakości (build/test)
4. Kolory, jasne komunikaty, instrukcje naprawy
5. Hook w repo = każdy klon ma go automatycznie

**Co nasz hook (hybrid-therapist) ma vs czego brakuje:**

| Funkcja | Stan | Uwagi |
|---------|------|-------|
| restore+build+test | ✅ | Blokujące |
| semgrep | ✅ | p/ci, non-blocking |
| AFDS docs | ✅ | Przez curl |
| format | ⚠️ | Dotnet-format build host bug |
| Staged-only | ❌ | Sprawdza całe repo |
| Branch naming | ❌ | Walidacja `feature/`, `fix/` |
| Commit message | ❌ | Conventional commits |
| Large file | ❌ | Blokada >1MB |
| Parallel exec | ❌ | Background jobs |

---

## TODOs

### Wave 1 — Struktura katalogu + pliki bazowe

- [ ] 1. Create directory structure

  ```bash
  mkdir -p /var/apps/ai-skills/skills/precommit-hook-architect/{templates,references}
  ```

- [ ] 2. Create `SKILL.md` — system prompt for AI agent

  **File**: `/var/apps/ai-skills/skills/precommit-hook-architect/SKILL.md`

  **Structure** (wzór: `ci-cd-architect/SKILL.md`):
  ```yaml
  ---
  name: precommit-hook-architect
  description: Expert AI persona for designing, auditing, and generating git precommit hooks for .NET, Python, Node.js, and polyglot projects. Enforces a single version-locked standard with config-driven generation, security scanning, and documentation validation.
  standard_version: 1.0.0
  ---
  ```

  **Sekcje**:
  - System Prompt / Persona — "You are the **Precommit Hook Architect**..."
  - Core Operating Directives (8-10 reguł)
  - Project Classification (język, framework, narzędzia → który szablon)
  - Operational Modes: AUDIT, GENERATE, FIX, EXTEND
  - Hook Step Reference (tabela: krok → komenda → poziom)

- [ ] 3. Create `precommit-standard.md` — standard rules

  **File**: `/var/apps/ai-skills/skills/precommit-hook-architect/precommit-standard.md`

  **Kluczowe sekcje**:
  - Rule levels: L1 (MUST/blokuje), L2 (SHOULD/można bypass), L3 (MAY/ostrzega)
  - Semantic anchors: `[HOOK-L1-BUILD]`, `[HOOK-L2-FORMAT]`, etc.
  - Step ordering rules (fast first, slow last)
  - Config contract schema (YAML/TOML)
  - Template selection matrix
  - Anti-patterns (czego NIE robić)

  **Przykładowe reguły**:
  - `[HOOK-L1-RESTORE]` — dotnet/npm/pip restore musi przejść
  - `[HOOK-L1-BUILD]` — build z 0 warningów
  - `[HOOK-L1-TEST]` — unit testy muszą przejść
  - `[HOOK-L2-FORMAT]` — format musi być czysty
  - `[HOOK-L2-SECURITY]` — semgrep na staged changes
  - `[HOOK-L3-DOCS]` — AFDS walidacja
  - `[HOOK-L3-BRANCH]` — walidacja nazwy brancha

- [ ] 4. Create `todo-precommit.md` — feature roadmap

  **File**: `/var/apps/ai-skills/skills/precommit-hook-architect/todo-precommit.md`

  **Zawartość**:

  **Faza 1 — Core** (zrobione w hybrid-therapist):
  - [x] Shell hook z set -euo pipefail
  - [x] Restore + Build + Test (blokujące)
  - [x] Semgrep (non-blocking)
  - [x] AFDS docs validation
  - [x] PATH setup (dotnet/python)

  **Faza 2 — Hardening:**
  - [ ] Staged-only checks
  - [ ] Parallel execution (background processes)
  - [ ] Timeout per step (30s/120s/300s)
  - [ ] Cache warmup (build cache save/restore)
  - [ ] Hook self-test (--self-test flag)
  - [ ] Auto-install script (make hooks lub install-hooks.sh)

  **Faza 3 — Advanced:**
  - [ ] Branch naming check (feature/, fix/, docs/)
  - [ ] Commit message validation (conventional commits)
  - [ ] Large file detection (>1MB)
  - [ ] Merge conflict markers (<<<<<<<)
  - [ ] Private key detection
  - [ ] Whitespace validation

  **Faza 4 — Multi-project:**
  - [ ] Jinja2 templates z config contract
  - [ ] Auto-detection (język, framework, narzędzia)
  - [ ] Plugin architecture
  - [ ] CI/CD sync (hook = mirror CI)
  - [ ] Cross-OS support

### Wave 2 — Templates

- [ ] 5. Create `pre-commit.j2` — .NET template

  **File**: `/var/apps/ai-skills/skills/precommit-hook-architect/templates/pre-commit.j2`

  **Features**:
  - Jinja2 variables: `project_name`, `dotnet_path`, `test_project`, `solution_file`
  - Configurable steps (loop over YAML/TOML config)
  - Color output, step timing
  - Bail-on-first-failure mode (fail_fast)

- [ ] 6. Create `pre-commit-python.j2` — Python variant

- [ ] 7. Create `hook-anti-patterns.md` — reference

  **File**: `/var/apps/ai-skills/skills/precommit-hook-architect/references/hook-anti-patterns.md`

  Antywzorce:
  - `|| true` na krokach jakościowych
  - Hook bez timeoutu (może wisieć w nieskończoność)
  - Hardcoded paths zamiast `$(git rev-parse --show-toplevel)`
  - Brak dokumentacji bypassu
  - Hook sprawdza całe repo zamiast staged-only
  - Ukrywanie błędów przez `2>/dev/null` na blokujących krokach

### Wave 3 — Integration + README

- [ ] 8. Update ai-skills README.md — add precommit-hook-architect to skill table

- [ ] 9. Self-validate: run skill against hybrid-therapist hook — AUDIT mode

---

## Commit Strategy

| Commit | Files |
|--------|-------|
| `feat: add precommit-hook-architect skill (SKILL.md + standard)` | SKILL.md, precommit-standard.md |
| `feat: add precommit templates and todo roadmap` | pre-commit.j2, pre-commit-python.j2, todo-precommit.md |
| `feat: add hook anti-patterns reference` | references/hook-anti-patterns.md |
| `docs: update README with precommit-hook-architect` | README.md |
