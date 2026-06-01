# H.A.N.D. Codec G - Benchmark Report

- **Date:** 2026-06-01T16:34:24Z
- **Commit:** 6c7022f
- **.NET:** 8.0.126
- **Mode:** all
- **StrictCodecG:** true
- **Prompt purity:** verified by tests

## Results

| Suite | Status | Passed | Failed |
|-------|--------|--------|--------|
| Unit tests | passed | 277 | 0 |
| Cassette HandBenchmark + mutations | passed | 19 | 0 |
| LiveOllama | passed | 1 | 0 |

## Token Savings

Status: `measured`

| Count | Avg % | Min % | Max % |
|-------|-------|-------|-------|
| 12 | 32.5 | 6.0 | 43.8 |

Token economy is parsed from runtime benchmark logs that measure L2/L3 memo wire
against expanded plaintext. If the current run did not emit those measurements,
the JSON report uses `not_measured` and `null` values.

## Artifacts

- Full report: [hand-codec-g.md](../../docs/benchmarks/hand-codec-g.md)
- Benchmark matrix: [benchmark-matrix.md](../../docs/benchmarks/benchmark-matrix.md)
- Cassette TRX: `/home/pablo/Projects/hybrid-therapist/artifacts/benchmarks/hand-benchmark.trx`
- Mutation TRX: `/home/pablo/Projects/hybrid-therapist/artifacts/benchmarks/hand-benchmark-negative.trx`
- Live TRX: `/home/pablo/Projects/hybrid-therapist/artifacts/benchmarks/hand-live.trx`
