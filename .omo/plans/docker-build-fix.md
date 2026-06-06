# Fix — Docker `image:` vs `build:` + dev loop hardening

## TL;DR

> **Root Cause**: `docker-compose.yml` używa `image: hybrid-therapist:local` bez `build:`. `docker compose up --build` NIE odbudowuje obrazu — kontener działał na starym kodzie sprzed wszystkich poprawek.
>
> **Deliverables**:
> - `docker-compose.yml`: dodane `build: .` do serwisu therapist
> - `AGENTS.md` / `README.md`: dokumentacja dev loop — jak przebudować po zmianach
> - `scripts/rebuild-therapist.sh`: skrypt odbudowujący obraz + restartujący kontener

---

## TODOs

- [x] 1. Add `build: .` to therapist service in docker-compose.yml
- [x] 2. Add rebuild script `scripts/rebuild-therapist.sh`
- [x] 3. Update AGENTS.md — document `build:` behaviour

  **What**: Dodać sekcję o tym, że obraz jest budowany z lokalnego kodu i trzeba go przebudować po zmianach .cs
  **File**: `AGENTS.md`

---

## Commit Strategy

| Commit | Files |
|--------|-------|
| `fix: add build: to therapist service + rebuild script` | docker-compose.yml, scripts/rebuild-therapist.sh, AGENTS.md |

---

## Why tests didn't catch it

| Warstwa testów | Co testuje | Wyłapuje ten bug? |
|---------------|-----------|-------------------|
| Unit (280) | Logika w izolacji | ❌ |
| Integration cassette (37) | Pipeline na mockowanych odpowiedziach | ❌ |
| Live Ollama E2E | Pełny pipeline z żywą Ollamą | ⚠️ tylko lokalnie |
| **Docker image staleness** | Brak testu | ❌ **root cause** |

`docker compose up -d --build` działałby poprawnie z `build:` w YAML-u.
Testy kasetowe NIE wyłapią nieaktualnego obrazu — one są deterministyczne, nie przez sieć Docker.
