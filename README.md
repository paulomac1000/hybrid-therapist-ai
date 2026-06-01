# Hybrid Therapist

Eksperymentalny, wieloagentowy psycholog AI działający w języku polskim.
Działa w całości lokalnie — bez chmury, bez opłat za tokeny, na jednej karcie graficznej za ~200 USD.

---

## Co to jest?

Hybrid Therapist używa **17-warstwowego pipeline'u Socrates** — zespołu sześciu wyspecjalizowanych,
lokalnych modeli LLM (przez Ollamę), z których każdy wykonuje jedno zadanie: tłumaczenie,
analizę emocji, planowanie terapeutyczne, generowanie odpowiedzi i kontrolę jakości.

Zamiast pytać jeden duży model "bądź psychologiem", pipeline rozdziela odpowiedzialność:
każdy model robi to, w czym jest najlepszy, a ich wyniki są łączone w spójną odpowiedź terapeutyczną.

**To nie jest zamiennik profesjonalnej terapii.** To eksperyment badawczy sprawdzający,
czy zespół małych, lokalnych modeli może działać lepiej w rozmowie terapeutycznej
niż pojedynczy model ogólnego przeznaczenia.

## Dlaczego wiele modeli zamiast jednego?

Pojedynczy model — nawet duży — musi jednocześnie:
- wykryć kryzys (czy użytkownik mówi o samobójstwie?)
- oczyścić dane osobowe
- zrozumieć stan emocjonalny
- wybrać strategię terapeutyczną
- wygenerować empatyczną odpowiedź po polsku
- sprawdzić jakość tej odpowiedzi

Pipeline Socrates rozkłada te zadania na osobne warstwy. Każda warstwa używa
modelu dostrojonego do swojego zadania (MentaLLaMA do analizy klinicznej,
PsyLLM do planowania terapii, PsychoCounsel do generowania odpowiedzi).

Dzięki temu:
- **Lepsza jakość** — każdy model robi jedną rzecz dobrze
- **Wykrywalne błędy** — ślad po każdej warstwie (trace), wiadomo co zawiodło
- **Lokalnie** — wszystko na Twoim sprzęcie, dane nie opuszczają maszyny
- **Tanio** — zero kosztów API, jedna karta graficzna

## Czym jest Socrates Pipeline?

```
Użytkownik (PL) → CrisisGate → PrivacySanitizer
                       ↓
                 L1 Tłumacz PL→EN (Bielik 7B)
                       ↓
                 L2 Analityk (MentaLLaMA 7B)  ← wykrywa emocje, nasilenie
                       ↓
                 L3 Supervisor (PsyLLM 8B)    ← wybiera strategię
                       ↓
                 L4 Terapeuta (PsychoCounsel 8B) ← generuje odpowiedź
                       ↓
                 L6 Kalibrator (Llama4-Dolphin 8B) ← poprawia styl
                       ↓
                 L7 Tłumacz EN→PL (Bielik 7B)
                       ↓
                 Użytkownik (odpowiedź po polsku)
```

Każda warstwa ma jedną odpowiedzialność:

| Warstwa | Co robi |
|---------|---------|
| CrisisGate | Wykrywa myśli samobójcze — zwraca numer 116 123, blokuje dalsze przetwarzanie |
| PrivacySanitizer | Usuwa e-maile, telefony, PESEL zanim trafią do LLM |
| L1/L7 Tłumacz | Tłumaczy PL↔EN (Bielik — polski model 7B) |
| L2 Analityk | Ocenia stan emocjonalny (MentaLLaMA — model kliniczny) |
| L3 Supervisor | Wybiera podejście terapeutyczne (PsyLLM — model terapeutyczny) |
| L4 Terapeuta | Generuje odpowiedź (PsychoCounsel — model doradczy) |
| L6 Kalibrator | Poprawia styl, usuwa sztampowe otwarcia |
| QA (EN + PL) | Sprawdza czy odpowiedź jest po polsku i nie zawiera wycieków promptu |

Warstwy komunikują się przez **H.A.N.D. Codec** — kompaktowy format wire, dzięki któremu
małe modele wymieniają dane kliniczne bez marnowania tokenów na rozwlekły plaintext.
Szczegóły protokołu: [docs/socrates-pipeline.md](docs/socrates-pipeline.md).

## Co potrafi

- **Pełna prywatność** — wszystko lokalnie, zero chmury, zero API zewnętrznych
- **Wykrywanie kryzysu** — twarde blokowanie myśli samobójczych, numer 116 123
- **Ochrona danych** — automatyczne usuwanie PESEL, e-maili, telefonów
- **Ślad diagnostyczny** — `/v1/trace/{sessionId}` pokazuje co zrobiła każda warstwa
- **API kompatybilne z OpenAI** — `/v1/chat/completions`, działa z LibreChat
- **Odporność na błędy** — awaria jednego modelu nie wywala całego pipeline'u
- **Świadomość fazy sesji** — inny styl na początku rozmowy, inny przy pogłębianiu

## H.A.N.D. Codec — eksperyment komunikacji międzyagentowej

Hybrid Therapist testuje [H.A.N.D. Codec](https://github.com/paulomac1000/hand-codec) jako protokół
komunikacji między małymi modelami. Obecny eksperyment **Codec G** używa losowo przemianowanych
kluczy (`e7`, `s9`, `p3`, `k2`...) — bez znaczenia semantycznego. L4 terapeuta otrzymuje surowe
linie `M|` **bez legendy i instrukcji formatu** — uczy się wzorca wyłącznie przez przykłady w
historii konwersacji (implicit priming).

Benchmark H.A.N.D. Codec G sprawdza teraz cały łańcuch: L2 generuje Codec G, L3
generuje Codec G, L4 dostaje surowe `M|` memo bez legendy, a finalna odpowiedź
pozostaje po polsku. Aktualne wyniki nie są wpisywane w README; generuje je:

```bash
./scripts/run-hand-benchmark.sh --cassette --report
```

[Pełny opis benchmarku](docs/benchmarks/hand-codec-g.md) | [Macierz porównawcza](docs/benchmarks/benchmark-matrix.md)

## Szybki start

```bash
# 1. Uruchom Ollamę i serwis terapeuty
docker compose up -d

# 2. Pobierz modele (pierwszy raz, ~25 GB)
docker compose exec ollama ollama pull SpeakLeash/bielik-minitron-7b-v3.0-instruct:Q4_K_M

# 3. Test — neutralny input
curl -X POST http://localhost:8080/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{"model":"hybrid-therapist","messages":[{"role":"user","content":"nie mogę zasnąć"}]}'

# 4. Test — CrisisGate blokuje niebezpieczny input
curl -X POST http://localhost:8080/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{"model":"hybrid-therapist","messages":[{"role":"user","content":"chcę skończyć z sobą"}]}'
# → odpowiedź zawiera "116 123" (polski telefon zaufania)
```

## Konfiguracja

Plik `config/stack.yaml` definiuje modele i ich role. W kodzie nie ma zahardcodowanych nazw modeli — wszystko idzie z konfiguracji.

```yaml
translator: SpeakLeash/bielik-minitron-7b-v3.0-instruct:Q4_K_M
analyst:    hf.co/mradermacher/MentaLLaMA-chat-7B-GGUF:Q4_K_M
supervisor: hf.co/RyanGichuru254/PsyLLM-8B-GGUF:Q4_K_M
therapist:  hf.co/mradermacher/PsychoCounsel-Llama3-8B-GGUF:Q4_K_S
calibrator: hf.co/mradermacher/llama4-dolphin-8B-GGUF:Q4_K_S
```

## Budowanie i testy

```bash
# Budowanie
dotnet build HybridTherapist.sln

# Testy jednostkowe (bez Ollamy)
dotnet test tests/HybridTherapist.Tests/

# Strict H.A.N.D. benchmark z kasetami (bez Ollamy, deterministyczny)
./scripts/run-hand-benchmark.sh --cassette --report

# Test E2E (wymaga Ollamy na localhost:11434)
OLLAMA_HOST=http://localhost:11434 dotnet test tests/HybridTherapist.Integration --filter "LiveOllama"
```

## Wymagania VRAM (GTX 1060 6GB)

Modele działają sekwencyjnie — tylko jeden załadowany w danym momencie. Szczytowe zużycie: ~4.9 GB (Supervisor).

| Warstwa | Model | VRAM |
|---------|-------|------|
| L1/L7 | Bielik 7B | 4.1 GB |
| L2 | MentaLLaMA 7B | 4.1 GB |
| L3 | PsyLLM 8B | 4.9 GB |
| L4 | PsychoCounsel 8B | 4.5 GB |
| L6 | Llama4-Dolphin 8B | 4.5 GB |

## Dokumentacja

- [docs/architecture.md](docs/architecture.md) — architektura, 17 warstw, przepływ danych
- [docs/socrates-pipeline.md](docs/socrates-pipeline.md) — protokół H.A.N.D., Implicit Priming, drabina odporności
- [docs/api.md](docs/api.md) — referencja API (OpenAI-compatible, SSE, trace)
- [docs/security.md](docs/security.md) — CrisisGate, PrivacySanitizer, niezmienniki bezpieczeństwa
- [docs/layer-necessity.md](docs/layer-necessity.md) — testy udowadniające konieczność każdej warstwy

## Status projektu

**Eksperymentalny.** Pipeline działa i ma testy regresji, ale nie był walidowany klinicznie.
Nie używać w sytuacjach wymagających profesjonalnej pomocy psychologicznej.

## Zależności zewnętrzne

- [HandCodec](https://github.com/paulomac1000/hand-codec) — kompaktowy format wire dla komunikacji między modelami
- [HandRuntime](https://github.com/paulomac1000/hand-codec) — orkiestracja warstw (Implicit Priming, drabina odporności)
- [Ollama](https://ollama.com) — lokalne uruchamianie modeli LLM

## Licencja

MIT
