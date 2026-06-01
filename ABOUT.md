# About Hybrid Therapist AI

## What is this?

Hybrid Therapist AI is an experimental multi-agent AI system that explores whether a **team of small, specialized local LLMs** can deliver therapeutic conversation quality comparable to a single large general-purpose model.

Instead of one model trying to do everything, the system uses a **17-layer Socrates pipeline** — six models orchestrated via the **H.A.N.D. Codec**, each responsible for a single task: translation, clinical analysis, therapeutic strategy, response generation, and quality control.

## Key innovations

- **Codec G** — an arbitrary, randomly-keyed inter-agent wire format (`e7`, `s9`, `p3`, `k2`) that models learn purely through checkpoint examples (implicit priming), without any format instruction in system prompts
- **Pure Implicit mode** — the therapist receives raw `M|` memo lines with zero legend or explanation, proving small models can negotiate emergent micro-languages
- **Strict H.A.N.D. benchmarks** — 11 scenario cassettes with trace-validated Codec G compliance, token economy measurement, and mutation tests

## What this is NOT

- ❌ A production-ready therapy application
- ❌ Clinically validated or reviewed by medical professionals
- ❌ A replacement for professional psychological help

## What this IS

- ✅ A research experiment in inter-agent communication protocols
- ✅ A proof of concept that small local models (7B–8B) can collaborate effectively
- ✅ A benchmark platform for testing implicit protocol learning

## Tech stack

- **Models:** Bielik 7B, MentaLLaMA 7B, PsyLLM 8B, PsychoCounsel 8B, Llama4-Dolphin 8B
- **Runtime:** Ollama (local-only, zero cloud)
- **Protocol:** [H.A.N.D. Codec](https://github.com/paulomac1000/hand-codec) v0.2.0
- **Framework:** .NET 8, ASP.NET Core
- **Frontend:** LibreChat (OpenAI-compatible API)

## Quick links

- [Architecture](docs/architecture.md)
- [Socrates Pipeline](docs/socrates-pipeline.md)
- [Codec G Benchmark](docs/benchmarks/hand-codec-g.md)
- [API Reference](docs/api.md)

## License

MIT
