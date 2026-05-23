# Hybrid Therapist

A self-contained, OpenAI-compatible Polish therapy AI using a Socrates multi-agent pipeline.
Communicates between layers via [HandCodec](../hand-codec) wire format.

## Architecture — Socrates Pipeline

```
User (Polish) → CrisisGate → PrivacySanitizer
                    ↓
              L1 Bielik PL→EN
                    ↓
              L2 MentaLLaMA (Analyst)
                    ↓
              L3 PsyLLM (Supervisor)
                    ↓
              L5 MemoryService (compaction)
                    ↓
              L4 PsychoCounsel (Therapist)
                    ↓
              L6 Llama4-Dolphin (Calibrator)
                    ↓
              L7 Bielik EN→PL
                    ↓
              User (Polish response)
```

**Layer order is enforced**: CrisisGate and PrivacySanitizer always execute first.

## Powered by H.A.N.D. Codec

The Socrates pipeline was built as a **living proof-of-concept** for the
[H.A.N.D. Codec](../hand-codec) — a probabilistic transport layer that lets small,
local LLMs communicate reliably in a structured pidgin language without being
instructed about the format.

### How the layers talk

Instead of verbose plaintext (which burns tokens and confuses small models),
the agents use two H.A.N.D. performatives:

**`M|` (Memo) — Analyst → Supervisor → Therapist.** The analyst emits a compact
clinical report in a single line:
```
M|L=2|em=anxiety|sv=moderate|ri=insomnia|cp=worry
```
The supervisor reads this raw wire, picks an approach, and emits its own memo:
```
M|L=3|ap=reflective_listening|tk=open_question|kq=What keeps you up at night?
```
Both memos feed directly into the therapist's prompt — raw, compact, zero expansion.
A dictionary key in the system prompt teaches the model to read the fields.

**`R|` (Result) — Therapist → Calibrator → User.** The therapist generates a response
with metadata on the first line and prose after a newline:
```
R|C=0.88
I hear that sleep has become a struggle for you. What tends to occupy your mind
when you lie down at night?
```
This Data/Narrative Split keeps the transformer's attention mechanism from hunting
for confidence scores buried at the end of a long therapeutic response.

### Implicit Priming — teaching by example, not instruction

The models were **never told** about H.A.N.D. No system prompt says "respond in format
R|C=...". Instead, before each LLM call, the orchestrator silently injects a single
non-therapeutic exchange into the conversation history:

```
User:      [SYSTEM_PROTOCOL_PING]
Assistant: R|C=1.0
           [SYSTEM_PROTOCOL_ACK]
```

The model sees the pattern and subconsciously continues it. It learns the format the
same way it learns anything — by **mimicking what it sees in context**. This is a
stateless cache: every call starts fresh with the same ping.

### Resilience — when small models stumble

Every layer runs the model's output through `HandResiliencePipeline` — a 5-level
degradation ladder. If the model writes perfect wire (Level 1), great. If it wraps
the wire in markdown fences (Level 3), the codec strips them. If it ignores the
format entirely and writes prose like *"The emotional state is anxiety..."* (Level 4),
regex extracts the fields and builds a valid memo. If everything fails (Level 5),
a safe fallback memo keeps the pipeline running. No HTTP 500. No crashes.

### Why this matters

Five small models. One $200 GPU. Zero cloud APIs. Zero per-token billing. They talk
to each other in a pidgin language they learned by imitation — and the pipeline
survives their mistakes gracefully. This is the promise of H.A.N.D. The therapist
is the proof.

## Quick start

```bash
# 1. Start Ollama and the therapist service
docker compose up -d

# 2. Pull required models (first time only, ~25 GB total)
docker exec hybrid-therapist-ollama-1 ollama pull SpeakLeash/bielik-minitron-7b-v3.0-instruct:Q4_K_M

# 3. Test with a non-crisis input
curl -X POST http://localhost:8080/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{"model":"hybrid-therapist","messages":[{"role":"user","content":"nie mogę zasnąć"}]}'

# 4. Verify crisis gate blocks dangerous input
curl -X POST http://localhost:8080/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{"model":"hybrid-therapist","messages":[{"role":"user","content":"chcę skończyć z sobą"}]}'
# → response will contain "116 123" (Polish crisis helpline)
```

## API

### `POST /v1/chat/completions`

OpenAI-compatible chat completions. Model: `hybrid-therapist`.

**Request:**
```json
{
  "model": "hybrid-therapist",
  "messages": [{"role": "user", "content": "nie mogę zasnąć"}]
}
```

**Response headers:**
- `X-Cortexa-Flow: hybrid-therapist`
- `X-Cortexa-Fallback: false` (true = L4 therapist or L7 translation failed, fallback returned)

### `GET /v1/models`

Returns `{"data": [{"id": "hybrid-therapist", ...}]}`.

## Configuration (`appsettings.json`)

```json
{
  "Ollama": { "BaseUrl": "http://localhost:11434" },
  "Models": {
    "Translator": "SpeakLeash/bielik-minitron-7b-v3.0-instruct:Q4_K_M",
    "Analyst": "hf.co/mradermacher/MentaLLaMA-chat-7B-GGUF:Q4_K_M",
    "Supervisor": "hf.co/RyanGichuru254/PsyLLM-8B-GGUF:Q4_K_M",
    "Therapist": "hf.co/mradermacher/PsychoCounsel-Llama3-8B-GGUF:Q4_K_S",
    "Calibrator": "hf.co/mradermacher/llama4-dolphin-8B-GGUF:Q4_K_S"
  }
}
```

Zero hardcoded model names in code. All model references come from configuration.

## Security

- **CrisisGate** runs before every LLM call. Hard-stops on Polish/English suicide phrases with a helpline response (116 123).
- **PrivacySanitizer** redacts email, phone, PESEL before any LLM call.
- Crisis detection keywords are in Polish — they must stay in Polish.

## HandCodec + HandRuntime dependency

The H.A.N.D. protocol libraries are published as NuGet packages on [GitHub Packages](https://github.com/paulomac1000/hand-codec/pkgs/nuget/HandCodec). Both repos are public — no authentication required for `dotnet restore`.

The `nuget.config` at the repository root configures two NuGet sources:
- `nuget.pkg.github.com/paulomac1000` — HandCodec + HandRuntime
- `nuget.org` — all other dependencies (YamlDotNet and others)

```xml
<PackageReference Include="HandCodec" Version="0.2.0" />
<PackageReference Include="HandRuntime" Version="0.2.0" />
```

`dotnet restore` resolves these automatically — no local `.nupkg` files needed.

## Documentation

- [docs/architecture.md](docs/architecture.md) — 5-project layout, 17-layer pipeline, data-flow table (full cortexa parity)
- [docs/socrates-pipeline.md](docs/socrates-pipeline.md) — Analyst + Supervisor + Therapist + Calibrator, M| Memo wire format, anti-hallucination guard
- [docs/layer-necessity.md](docs/layer-necessity.md) — per-layer test contracts proving each layer earns its place
- [docs/security.md](docs/security.md) — CrisisGate, PrivacySanitizer, invariants and test requirements
- [docs/api.md](docs/api.md) — HTTP API reference, SSE streaming, `/v1/trace/{sessionId}` debugging endpoint

## Building and testing

```bash
# Build
dotnet build HybridTherapist.sln

# Unit tests (no Ollama required, 198+ tests)
dotnet test tests/HybridTherapist.Tests/

# Cassette integration tests (no Ollama required, deterministic with WireMock)
dotnet test tests/HybridTherapist.Integration --filter "Cassette"

# E2E integration test (requires Ollama on localhost:11434)
OLLAMA_HOST=http://localhost:11434 dotnet test tests/HybridTherapist.Integration --filter "LiveOllama"
```

## VRAM requirements (GTX 1060 6GB)

Models run sequentially — only one model loaded at a time. Peak: ~4.9 GB (Supervisor layer).

| Layer | Model | VRAM |
|-------|-------|------|
| L1/L7 | Bielik 7B | 4.1 GB |
| L2 | MentaLLaMA 7B | 4.1 GB |
| L3 | PsyLLM 8B | 4.9 GB |
| L4 | PsychoCounsel 8B | 4.5 GB |
| L6 | Llama4-Dolphin 8B | 4.5 GB |

## License

MIT
