# Skill Audit + Adapt — precommit-hook-architect

## TL;DR

> **Quick Summary**: Audyt skilla `precommit-hook-architect` v1.0.0 z perspektywy `hybrid-therapist` — 87% zgodności. 4 luki do zaadoptowania w projekcie. Audit wynikający z analizy zapisany w `/var/apps/ai-skills/audit.md`.
>
> **Deliverables**:
> - `/var/apps/ai-skills/audit.md` — raport audytu skilla (zapisany oddzielnie)
> - `.github/precommit-config.yaml` — config contract dla hooka (nowy)
> - `.githooks/pre-commit` — +secret detection +trailing whitespace (2 nowe kroki)
> - AGENTS.md — +skill reference update (opcjonalne)
>
> **Estimated Effort**: Quick (~30 min)
> **Parallel Execution**: YES

---

## Context

### Audit Wynik
Skill `precommit-hook-architect` v1.0.0 przeanalizowany w kontekście `hybrid-therapist/.githooks/pre-commit`:

| Obszar | Zgodność | Uwagi |
|--------|----------|-------|
| Semantic Anchors (10/10) | 100% | Wszystkie HOOK-L1/L2/L3 obecne |
| Core Directives (9/10) | 90% | #7 Config-Driven — brak config contract |
| Anti-Patterns | 0 violations | Hook czysty |
| Skill → Hook alignment | 87% overall |

### Luki do zaadoptowania w projekcie

1. **[HOOK-L2-SECRET]** — Private key / secret detection (brakuje)
   Standard: "Catch secrets before commit". Wzór: `grep -E 'BEGIN.*PRIVATE KEY|-----BEGIN'` na staged files.
   Wartość dla projektu: chroni przed wyciekiem kluczy API, tokenów, certyfikatów.

2. **[HOOK-L2-TRAILING]** — Trailing whitespace (brakuje)  
   Standard nie definiuje explicite, ale antywzorzec #4 (staged-only) implikuje że wszystkie staged pliki powinny być czyste. Trailing whitespace to najczęstszy "code smell" w diffach.
   Wartość: czystsze diffs, mniej szumu w code review.

3. **[CONFIG-CONTRACT]** — `.github/precommit-config.yaml` (brakuje)
   Core Directive #7: "All project parameters MUST come from a config contract."
   Obecnie: solution path, test project, dotnet path są zahardcodowane w hooku.
   Wartość: hook staje się przenośny między projektami .NET.

4. **[SKILL-GAP]** — `ci-cd-standard.md` nie istnieje
   `precommit-standard.md` linia 16 odwołuje się do `ci-cd-standard.md` który nie istnieje w `/var/apps/ai-skills/skills/`. To luka w ekosystemie skilli — do zgłoszenia w audit.md.

### Co NIE wymaga adaptacji (hook już ma)
- Timeouts ✅ (wszystkie kroki)
- Staged-only ✅ (format + semgrep)
- --self-test ✅
- Merge conflict detection ✅
- Large file detection ✅
- Branch naming ✅ (pre-push)
- Conventional commits ✅ (commit-msg)
- Install script ✅
- CI-awareness ✅ (format blokujący na CI, nieblokujący lokalnie)

### Co skill ma lepiej niż hook
- Config contract (`.github/precommit-config.yaml`) — hook go nie ma
- Template-based generation (Jinja2) — hook jest hand-written

### Co hook ma lepiej niż skill
- CI-awareness dla format step (brakuje w skillu jako wzorzec)
- Pre-push + commit-msg hooki (skill wspomina o nich w Directive #9 ale nie ma template'ów)
- Staged-only dla semgrep (skill pomija)

---

## Work Objectives

### Core Objective
Zamknąć 3 luki w hooku (secret detection, trailing whitespace, config contract) + zgłosić 1 lukę w skillu (brak ci-cd-standard.md).

### Concrete Deliverables
1. `.github/precommit-config.yaml` — config contract z parametrami projektu
2. `.githooks/pre-commit` — +2 nowe kroki: secret detection, trailing whitespace
3. `/var/apps/ai-skills/audit.md` — raport audytu (zapisany oddzielnym taskiem)
4. Opcjonalnie: AGENTS.md reference update

---

## Tasks

### Wave 1 — Audit Report (write to ai-skills)

- [x] 1. Stwórz `/var/apps/ai-skills/audit.md` z raportem audytu
  **Zawartość**: Pełny raport HOOK AUDIT w formacie zdefiniowanym przez skill (SKILL.md AUDIT Mode)
  **Format**: 
  ```
  HOOK AUDIT REPORT — hybrid-therapist
  ═════════════════════════════════════
  Skill:       precommit-hook-architect v1.0.0
  Project:     .NET 10 + xUnit + Semgrep + AFDS
  Hook:        .githooks/pre-commit (150 lines)
  Compliance:  9/10 core directives (90%)
               10/10 semantic anchors (100%)
               0 anti-pattern violations
  
  VIOLATIONS:
    [CONFIG-CONTRACT] — Core Directive #7: config contract missing
  
  PASSING:
    (wszystkie 10 semantic anchors + 9/10 directives)
  
  GAPS DETECTED (things hook misses):
    [HOOK-L2-SECRET] — no private key/secret detection
    [HOOK-L2-TRAILING] — no trailing whitespace check
  
  SKILL GAPS DETECTED (things skill misses):
    [SKILL-GAP-CICD] — ci-cd-standard.md referenced but doesn't exist
    [SKILL-GAP-CI-AWARE] — CI-awareness format pattern not documented as best practice
    [SKILL-GAP-STAGED-SEMGREP] — staged-only for semgrep not mentioned
  
  RECOMMENDATIONS:
    1. Add secret detection to hook
    2. Add trailing whitespace check to hook
    3. Create .github/precommit-config.yaml
    4. Create ci-cd-standard.md in ai-skills
    5. Add CI-awareness pattern to SKILL.md examples
  ```
  **Plik**: `/var/apps/ai-skills/audit.md`

### Wave 2 — Adapt new elements to project

- [x] 2. Dodaj secret/private key detection do hooka (HOOK-L2-SECRET)
  **Co**: Nowy krok (pre-check, L2, timeout 10s) skanujący staged files na `BEGIN.*PRIVATE KEY`, `-----BEGIN RSA`, `-----BEGIN OPENSSH`, `api_key\s*=\s*['\"]\w{20,}`
  **Pozycja**: Pre-check (przed restore), obok merge conflict detection
  **Plik**: `.githooks/pre-commit`

- [x] 3. Dodaj trailing whitespace check do hooka
  **Co**: `git diff --cached --check` — wbudowane w git, wykrywa trailing whitespace + conflict markers
  **Pozycja**: Pre-check (2 sekundy), L3 (ostrzega, nie blokuje)
  **Plik**: `.githooks/pre-commit`

- [x] 4. Stwórz `.github/precommit-config.yaml`
  **Co**: YAML config contract z parametrami hooka — solution, test project, dotnet path, timeouty, enabled/disabled flagi per krok
  **Wzorzec**: z `precommit-standard.md` linie 118-164
  **Plik**: `.github/precommit-config.yaml` (nowy)

---

## Final Verification Wave

- [x] F1. **Hook syntax** — `bash -n .githooks/pre-commit` → no errors
- [x] F2. **Self-test** — `.githooks/pre-commit --self-test` → all steps pass
- [x] F3. **Secret detection** — `echo "BEGIN PRIVATE KEY" > test && git add test && .githooks/pre-commit` → fails
- [x] F4. **Build + tests** — 0W/0E, 283+/283+ pass
- [x] F5. **Audit.md exists** — `/var/apps/ai-skills/audit.md` contains the audit report

---

## Execution Strategy

```
Wave 1: Audit Report (2 min — write to ai-skills)
└── Task 1: /var/apps/ai-skills/audit.md

Wave 2: Adapt (20 min)
├── Task 2: Secret detection (hook update)
├── Task 3: Trailing whitespace (hook update)
└── Task 4: Config contract (new file)

Critical Path: Wave 1 → Wave 2 → Final Wave
```
