# Precommit Hook — Skill ↔ Repo Bridge

## TL;DR

> **Quick Summary**: Ujednolicenie `.githooks/pre-commit` z `precommit-hook-architect` standardem v1.0.0 + uzupełnienie obu (hooka i skilla) o znalezione luki. Faza 2 roadmapy (staged-only, timeouts, self-test) + Faza 3 (merge conflicts, large files, branch naming jako pre-push). Skill update: fix 2>/dev/null anti-pattern, staged-only support w j2, $HOME/.dotnet fallback, CI-awareness pattern jako good example.
>
> **Deliverables**:
> - `.githooks/pre-commit` zaktualizowany: +staged-only +timeouts +merge-conflicts +large-files -2>/dev/null
> - Skill update: `pre-commit.j2` template + `precommit-standard.md` CI-awareness note + `hook-anti-patterns.md` new patterns
> - Nowy plik: `.githooks/pre-push` (branch naming validation)
> - Nowy plik: `.githooks/commit-msg` (conventional commits validation)
> - Nowy plik: `scripts/install-hooks.sh` (auto-install)
>
> **Estimated Effort**: ~3h (2 fale: hook fix + skill fix)
> **Parallel Execution**: YES — hook i skill są niezależne

---

## Context

### Background
- **Skill**: `/var/apps/ai-skills/skills/precommit-hook-architect/` — SKILL.md, precommit-standard.md v1.0.0, todo-precommit.md, templates/pre-commit.j2, references/hook-anti-patterns.md
- **Nasz hook**: `/home/pablo/Projects/hybrid-therapist/.githooks/pre-commit` — 97 linii, 6 kroków: restore → format → build → test → semgrep → AFDS
- **Standard**: 10 Core Operating Directives, 9 Semantic Anchors (HOOK-L1/L2/L3), 5-level step ordering, Config Contract
- **Roadmap**: 5 faz (Faza 1 done, Fazy 2-5 pending)

### Current Compliance
| Rule | Standard | Hook | Status |
|------|----------|------|--------|
| [HOOK-L1-RESTORE] | L1, block | ✅ restore with fail | OK |
| [HOOK-L1-BUILD] | L1, block | ✅ build with fail | OK |
| [HOOK-L1-TEST] | L1, block | ✅ test with fail | OK |
| [HOOK-L2-FORMAT] | L2, SHOULD | ✅ format + CI-awareness | OK (clever) |
| [HOOK-L2-SECURITY] | L2, SHOULD | ✅ semgrep non-blocking | OK |
| [HOOK-L3-DOCS] | L3, MAY | ✅ AFDS non-blocking | OK |
| Staged-only | MUST | ❌ full-repo | **GAP** |
| Timeouts | MUST | ❌ no timeouts | **GAP** |
| 2>/dev/null | ANTI-PATTERN | ❌ on ALL steps | **GAP** |
| Self-test flag | MUST | ❌ none | **GAP** |
| Merge conflicts | L1 | ❌ not checked | **GAP** |
| Large files | L2 | ❌ not checked | **GAP** |
| Branch naming | L3 | ❌ not checked | **GAP** |
| Commit message | L3 | ❌ not checked | **GAP** |
| Config file | SHOULD | ❌ hardcoded | **GAP** |

### Research Findings
- **Timeouts**: Industry standard. Pre-commit framework (`pre-commit/pre-commit`) has built-in timeout. Reddit/GitHub consensus: 30s format, 120s build, 300s test.
- **Staged-only**: Semgrep v1.28+ supports diff-aware scan. Format/lint should ONLY run on staged files — full-repo checks discourage frequent commits.
- **Branch naming**: Reddit consensus — validate on `pre-push`, NOT `pre-commit`. Developers rename locally; what matters is what reaches the server.
- **Conventional commits**: Should be `commit-msg` hook, NOT `pre-commit`. Commitizen v4.1 is the tool of choice.
- **2>/dev/null**: The standard explicitly forbids it on blocking steps. Our hook uses it everywhere — needs rewriting.
- **DOTNET fallback**: Our `$HOME/.dotnet` pattern isn't in the j2 template but is battle-proven and should be added to the skill.

---

## Work Objectives

### Core Objective
Zamknąć luki między hookiem a standardem, wdrożyć Fazę 2+3 roadmapy, i uzupełnić skill o brakujące wzorce.

### Concrete Deliverables
1. Hook: staged-only execution na format + semgrep
2. Hook: timeouts na każdym kroku (30s/120s/300s)
3. Hook: usunięcie `2>/dev/null` z blokujących kroków
4. Hook: merge conflict detection (L1)
5. Hook: large file detection (L2)
6. Pre-push: branch naming validation (L3, regex-based)
7. Commit-msg: conventional commits validation (L3)
8. Script: `scripts/install-hooks.sh` (auto-install)
9. Skill: j2 template — staged-only support + $HOME/.dotnet fallback
10. Skill: anti-patterns — add "2>/dev/null everywhere" + "full-repo instead of staged"
11. Skill: SKILL.md — mention pre-push and commit-msg in hook step reference
12. Skill: todo-precommit.md — mark completed items

### Definition of Done
- [x] Hook passes `bash -n` syntax check
- [x] Hook passes self-test: `./githooks/pre-commit --self-test` (all steps pass)
- [x] Pre-push validates branch names
- [x] Commit-msg validates conventional commits format
- [x] `scripts/install-hooks.sh` --install and --uninstall work
- [x] Skill files updated and consistent with standard v1.0.0
- [x] Build: 0W/0E, Unit tests: 283+/283+ pass

---

## Tasks

### Wave 1 — Hook Fixes (2h)

- [x] 1. Proxyj 2>/dev/null z blokujących kroków
  **Problem**: Każdy krok ma `2>/dev/null` lub `&>/dev/null` — narusza [HOOK-ANTI-2]
  **Fix**: Usuń `2>/dev/null` z restore, build, test. Dla format i semgrep (non-blocking) — poprawnie pokazuj output.
  **Co konkretnie**: Zmień `dotnet restore --nologo -v q 2>/dev/null` → `dotnet restore --nologo -v q`. Podobnie dla build, test.
  **Nie tykaj**: format (2>/dev/null jest OK dla non-blocking stepu), AFDS (potrzebuje pipe do python)
  **Plik**: `.githooks/pre-commit`

- [x] 2. Dodaj timeouts na każdym kroku
  **Co**: `timeout 30`, `timeout 120`, `timeout 300` zgodnie ze standardem
  **Fix**:
  - Restore: `timeout 60`
  - Format: `timeout 30`
  - Build: `timeout 120`
  - Test: `timeout 300`
  - Semgrep: `timeout 60`
  - AFDS: `timeout 30`
  **Plik**: `.githooks/pre-commit`

- [x] 3. Dodaj staged-only execution dla format i semgrep
  **Problem**: Format lintuje całe repo — wolne, zniechęca do commitów
  **Fix**: Dodaj `STAGED_CS=$(git diff --cached --name-only --diff-filter=ACM -- "*.cs")` przed format step. Jeśli puste — skip. Podobnie dla semgrep.
  **Plik**: `.githooks/pre-commit`

- [x] 4. Dodaj merge conflict detection (L1, blocking)
  **Co**: `grep -rn '<<<<<<<\|=======\|>>>>>>>' --include='*.cs' --include='*.py' --include='*.sh'` na staged files.
  **Lokalizacja**: Przed restore (pre-check, position 0)
  **Plik**: `.githooks/pre-commit`

- [x] 5. Dodaj large file detection (L2, blocking z bypass)
  **Co**: Scan staged files >1MB: `git diff --cached --name-only --diff-filter=ACM | xargs -I{} ls -l {} 2>/dev/null | awk '$5 > 1048576 {print $NF, $5/1024/1024 "MB"}'`
  **Lokalizacja**: Pre-check, position 0
  **Plik**: `.githooks/pre-commit`

- [x] 6. Dodaj `--self-test` flagę
  **Co**: Jeśli `$1 == "--self-test"`, uruchom wszystkie kroki z `EXIT_ON_FAIL=false` i timeout=5s per krok, raportuj PASS/FAIL per krok.
  **Plik**: `.githooks/pre-commit`

### Wave 2 — Nowe hooki + Skill update (1h)

- [x] 7. Stwórz `.githooks/pre-push` — branch naming validation
  **Co**: Przed push, sprawdź czy branch name pasuje do `^(feature\|fix\|docs\|chore\|refactor\|cleanup)/`
  **Poziom**: L3 (MAY) — ostrzegaj, nie blokuj
  **Wyjście**: `"⚠ Branch '$BRANCH' doesn't match naming convention"`
  **Plik**: `.githooks/pre-push` (nowy)

- [x] 8. Stwórz `.githooks/commit-msg` — conventional commits
  **Co**: Sprawdź czy commit message pasuje do `^(feat\|fix\|docs\|chore\|refactor\|test\|build\|deps)(\(.+\))?: `
  **Poziom**: L3 (MAY) — ostrzegaj, nie blokuj
  **Plik**: `.githooks/commit-msg` (nowy)

- [x] 9. Stwórz `scripts/install-hooks.sh`
  **Co**: `./scripts/install-hooks.sh` → `git config core.hooksPath .githooks && chmod +x .githooks/*`
  **Z argumentem**: `--uninstall` → `git config --unset core.hooksPath`
  **Plik**: `scripts/install-hooks.sh` (nowy)

- [x] 10. Zaktualizuj `pre-commit.j2` template w skillu
  **Fixy**:
  - Dodaj `$HOME/.dotnet/dotnet` fallback (nasz battle-proven pattern)
  - Dodaj staged-only support dla format step
  - Dodaj `--self-test` flagę z szybkim testem per krok
  - Dodaj pre-check timeouts (branch naming, merge conflicts, large files) jako opcjonalne bloki
  **Plik**: `/var/apps/ai-skills/skills/precommit-hook-architect/templates/pre-commit.j2`

- [x] 11. Zaktualizuj `hook-anti-patterns.md` w skillu
  **Nowe antywzorce**:
  - "2>/dev/null on ALL steps (not just non-blocking)" — flag the pattern seen in our original hook
  - "Full-repo format without staged-only guard" — add code example
  - "Hardcoded dotnet path without $HOME/.dotnet fallback" — add our GPU-host pattern as GOOD example
  **Plik**: `/var/apps/ai-skills/skills/precommit-hook-architect/references/hook-anti-patterns.md`

- [x] 12. Zaktualizuj `todo-precommit.md` — mark completed items
  **Faza 2 done**: staged-only, timeouts, self-test, install script
  **Faza 3 done**: branch naming (pre-push), commit message (commit-msg), merge conflicts, large files
  **Plik**: `/var/apps/ai-skills/skills/precommit-hook-architect/todo-precommit.md`

---

## Final Verification Wave

- [x] F1. **Hook syntax** — `bash -n .githooks/pre-commit .githooks/pre-push .githooks/commit-msg scripts/install-hooks.sh` → no errors
- [x] F2. **Hook self-test** — `.githooks/pre-commit --self-test` → ALL STEPS PASSED
- [x] F3. **Real commit** — `git add` + `git commit` → hook passes all checks
- [x] F4. **Install script** — `./scripts/install-hooks.sh` → `git config core.hooksPath` = `.githooks`
- [x] F5. **Build + tests** — `dotnet build -c Release` 0W/0E, `dotnet test` 283+/283+

---

## Execution Strategy

```
Wave 1: Hook Fixes (2h)
├── 1. Proxyj 2>/dev/null
├── 2. Dodaj timeouts
├── 3. Staged-only execution
├── 4. Merge conflict detection
├── 5. Large file detection
└── 6. --self-test flag

Wave 2: New hooks + Skill update (1h) — ALL PARALLEL
├── 7. Pre-push (branch naming)
├── 8. Commit-msg (conventional commits)
├── 9. Install script
├── 10. Skill j2 template update
├── 11. Skill anti-patterns update
└── 12. Skill todo update

Critical Path: Wave 1 → Wave 2 → Final Wave
```
