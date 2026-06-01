#!/usr/bin/env bash
set -euo pipefail

# H.A.N.D. Codec G Benchmark Runner
#
# Usage:
#   ./scripts/run-hand-benchmark.sh                    # cassette mode (default, no Docker)
#   ./scripts/run-hand-benchmark.sh --cassette          # cassette mode explicit
#   ./scripts/run-hand-benchmark.sh --live              # live mode (requires Docker + Ollama)
#   ./scripts/run-hand-benchmark.sh --all               # cassette + live
#   ./scripts/run-hand-benchmark.sh --cassette --report # generate artifacts
#
# Output:
#   artifacts/benchmarks/hand-codec-g-latest.json
#   artifacts/benchmarks/hand-codec-g-latest.md
#   artifacts/benchmarks/hand-benchmark.trx
#   artifacts/benchmarks/hand-live.trx

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARTIFACTS_DIR="$REPO_ROOT/artifacts/benchmarks"
TIMESTAMP=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
COMMIT_SHA=$(git -C "$REPO_ROOT" rev-parse --short HEAD 2>/dev/null || echo "unknown")
DOTNET_VERSION=$(dotnet --version 2>/dev/null || echo "unknown")
REPORT_FLAG=false
MODE="cassette"

for arg in "$@"; do
    case "$arg" in
        --cassette) MODE="cassette" ;;
        --live) MODE="live" ;;
        --all) MODE="all" ;;
        --report) REPORT_FLAG=true ;;
        --help|-h)
            echo "Usage: $0 [--cassette|--live|--all] [--report]"
            echo "  --cassette  Cassette-only benchmark (no Docker/Ollama) - default"
            echo "  --live      Live benchmark (requires Docker Compose + Ollama)"
            echo "  --all       Run both cassette and live benchmarks"
            echo "  --report    Generate JSON + Markdown report artifacts"
            exit 0
            ;;
        *)
            echo "Unknown argument: $arg" >&2
            exit 2
            ;;
    esac
done

RUN_CASSETTE=false
RUN_LIVE=false
case "$MODE" in
    cassette) RUN_CASSETTE=true ;;
    live) RUN_LIVE=true ;;
    all) RUN_CASSETTE=true; RUN_LIVE=true ;;
esac

echo "== H.A.N.D. Codec G Benchmark =="
echo "  mode:      $MODE"
echo "  commit:    $COMMIT_SHA"
echo "  .NET:      $DOTNET_VERSION"
echo "  timestamp: $TIMESTAMP"
echo ""

mkdir -p "$ARTIFACTS_DIR"

extract_passed() {
    printf '%s\n' "$1" | grep -oP 'Passed:\s*\K\d+' | tail -1 || echo "0"
}

extract_failed() {
    printf '%s\n' "$1" | grep -oP 'Failed:\s*\K\d+' | tail -1 || echo "0"
}

json_number_or_null() {
    if [ -n "${1:-}" ]; then
        printf '%s' "$1"
    else
        printf 'null'
    fi
}

parse_token_savings() {
    local output="$1"
    local values
    values=$(printf '%s\n' "$output" | grep -oP 'tokens saved \(\K[-0-9.]+(?=%\))' || true)
    if [ -z "$values" ]; then
        TOKEN_SAVINGS_STATUS="not_measured"
        TOKEN_SAVINGS_COUNT=""
        TOKEN_SAVINGS_AVG=""
        TOKEN_SAVINGS_MIN=""
        TOKEN_SAVINGS_MAX=""
        return
    fi

    TOKEN_SAVINGS_STATUS="measured"
    TOKEN_SAVINGS_COUNT=$(printf '%s\n' "$values" | awk 'NF { count++ } END { print count + 0 }')
    TOKEN_SAVINGS_AVG=$(printf '%s\n' "$values" | awk 'NF { sum += $1; count++ } END { if (count > 0) printf "%.1f", sum / count }')
    TOKEN_SAVINGS_MIN=$(printf '%s\n' "$values" | awk 'NF { if (count == 0 || $1 < min) min = $1; count++ } END { if (count > 0) printf "%.1f", min }')
    TOKEN_SAVINGS_MAX=$(printf '%s\n' "$values" | awk 'NF { if (count == 0 || $1 > max) max = $1; count++ } END { if (count > 0) printf "%.1f", max }')
}

# 1. Check Docker services only for live mode.
if [ "$RUN_LIVE" = true ]; then
    echo "1. Checking Docker services..."
    if ! docker compose -f "$REPO_ROOT/docker-compose.yml" ps --format json 2>/dev/null | grep -q "ollama"; then
        echo "   ERROR: Docker Compose stack is not running."
        echo "   Run: docker compose up -d"
        exit 1
    fi
    echo "   OK"

    echo "2. Checking Ollama..."
    OLLAMA_URL="http://localhost:11434"
    if ! curl -fsS "$OLLAMA_URL/api/tags" >/dev/null 2>&1; then
        echo "   ERROR: Ollama not reachable at $OLLAMA_URL"
        exit 1
    fi
    echo "   OK"
fi

# 3. Build solution.
echo "3. Building solution..."
dotnet build "$REPO_ROOT/HybridTherapist.sln" -c Release --nologo -v q 2>&1
echo "   OK"

# 4. Run unit tests.
echo "4. Running unit tests..."
set +e
UNIT_RESULT=$(dotnet test "$REPO_ROOT/tests/HybridTherapist.Tests" -c Release --no-build --nologo 2>&1)
UNIT_EXIT=$?
set -e
UNIT_PASSED=$(extract_passed "$UNIT_RESULT")
UNIT_FAILED=$(extract_failed "$UNIT_RESULT")
if [ "$UNIT_EXIT" -ne 0 ] && [ "$UNIT_FAILED" -eq 0 ]; then UNIT_FAILED=1; fi
echo "   Passed: $UNIT_PASSED  Failed: $UNIT_FAILED"

CASSETTE_RESULT=""
CASSETTE_PASSED=0
CASSETTE_FAILED=0
NEGATIVE_RESULT=""
NEGATIVE_PASSED=0
NEGATIVE_FAILED=0
CASSETTE_STATUS="not_run"
if [ "$RUN_CASSETTE" = true ]; then
    echo "5. Running cassette H.A.N.D. benchmark..."
    set +e
    CASSETTE_RESULT=$(dotnet test "$REPO_ROOT/tests/HybridTherapist.Integration" -c Release --no-build --nologo \
        --filter "FullyQualifiedName~HandBenchmarkTests" \
        --logger "trx;LogFileName=$ARTIFACTS_DIR/hand-benchmark.trx" 2>&1)
    CASSETTE_EXIT=$?
    set -e
    CASSETTE_PASSED=$(extract_passed "$CASSETTE_RESULT")
    CASSETTE_FAILED=$(extract_failed "$CASSETTE_RESULT")
    if [ "$CASSETTE_EXIT" -ne 0 ] && [ "$CASSETTE_FAILED" -eq 0 ]; then CASSETTE_FAILED=1; fi

    echo "   Running negative mutation tests..."
    set +e
    NEGATIVE_RESULT=$(dotnet test "$REPO_ROOT/tests/HybridTherapist.Integration" -c Release --no-build --nologo \
        --filter "FullyQualifiedName~HandBenchmarkNegativeTests" \
        --logger "trx;LogFileName=$ARTIFACTS_DIR/hand-benchmark-negative.trx" 2>&1)
    NEGATIVE_EXIT=$?
    set -e
    NEGATIVE_PASSED=$(extract_passed "$NEGATIVE_RESULT")
    NEGATIVE_FAILED=$(extract_failed "$NEGATIVE_RESULT")
    if [ "$NEGATIVE_EXIT" -ne 0 ] && [ "$NEGATIVE_FAILED" -eq 0 ]; then NEGATIVE_FAILED=1; fi

    CASSETTE_STATUS=$([ "$CASSETTE_EXIT" -eq 0 ] && [ "$NEGATIVE_EXIT" -eq 0 ] && echo "passed" || echo "failed")
    CASSETTE_PASSED=$((CASSETTE_PASSED + NEGATIVE_PASSED))
    CASSETTE_FAILED=$((CASSETTE_FAILED + NEGATIVE_FAILED))
    echo "   Passed: $CASSETTE_PASSED  Failed: $CASSETTE_FAILED"
fi

LIVE_RESULT=""
LIVE_PASSED=0
LIVE_FAILED=0
LIVE_STATUS="not_run"
if [ "$RUN_LIVE" = true ]; then
    echo "6. Running live H.A.N.D. benchmark..."
    set +e
    LIVE_RESULT=$(OLLAMA_HOST=http://localhost:11434 dotnet test "$REPO_ROOT/tests/HybridTherapist.Integration" -c Release --no-build --nologo \
        --filter "LiveOllama" \
        --logger "trx;LogFileName=$ARTIFACTS_DIR/hand-live.trx" 2>&1)
    LIVE_EXIT=$?
    set -e
    LIVE_PASSED=$(extract_passed "$LIVE_RESULT")
    LIVE_FAILED=$(extract_failed "$LIVE_RESULT")
    if [ "$LIVE_EXIT" -ne 0 ] && [ "$LIVE_FAILED" -eq 0 ]; then LIVE_FAILED=1; fi
    LIVE_STATUS=$([ "$LIVE_EXIT" -eq 0 ] && echo "passed" || echo "failed")
    echo "   Passed: $LIVE_PASSED  Failed: $LIVE_FAILED"
fi

parse_token_savings "$CASSETTE_RESULT
$LIVE_RESULT"

echo ""
echo "=== Summary ==="
echo "  Unit tests:     $UNIT_PASSED passed, $UNIT_FAILED failed"
echo "  Cassette:       $CASSETTE_STATUS ($CASSETTE_PASSED passed, $CASSETTE_FAILED failed)"
echo "  Live:           $LIVE_STATUS ($LIVE_PASSED passed, $LIVE_FAILED failed)"
echo "  Token savings:  $TOKEN_SAVINGS_STATUS"
echo "  Artifacts:      $ARTIFACTS_DIR"
echo ""

TOTAL_FAILED=$((UNIT_FAILED + CASSETTE_FAILED + LIVE_FAILED))
if [ "$TOTAL_FAILED" -gt 0 ]; then
    echo "BENCHMARK FAILED"
    exit 1
fi

echo "BENCHMARK PASSED"

if [ "$REPORT_FLAG" = true ]; then
    echo ""
    echo "7. Generating report artifacts..."

    TOKEN_AVG_JSON=$(json_number_or_null "$TOKEN_SAVINGS_AVG")
    TOKEN_MIN_JSON=$(json_number_or_null "$TOKEN_SAVINGS_MIN")
    TOKEN_MAX_JSON=$(json_number_or_null "$TOKEN_SAVINGS_MAX")
    TOKEN_COUNT_JSON=$(json_number_or_null "$TOKEN_SAVINGS_COUNT")

    cat > "$ARTIFACTS_DIR/hand-codec-g-latest.json" <<JSONEOF
{
  "benchmark": "hand-codec-g",
  "timestamp_utc": "$TIMESTAMP",
  "commit": "$COMMIT_SHA",
  "dotnet_version": "$DOTNET_VERSION",
  "strict_codec_g": true,
  "prompt_purity_status": "verified_by_tests",
  "mode": "$MODE",
  "unit_tests": {
    "passed": $UNIT_PASSED,
    "failed": $UNIT_FAILED
  },
  "cassette_benchmark": {
    "status": "$CASSETTE_STATUS",
    "passed": $CASSETTE_PASSED,
    "failed": $CASSETTE_FAILED,
    "trx": "$ARTIFACTS_DIR/hand-benchmark.trx",
    "negative_trx": "$ARTIFACTS_DIR/hand-benchmark-negative.trx"
  },
  "live_benchmark": {
    "status": "$LIVE_STATUS",
    "passed": $LIVE_PASSED,
    "failed": $LIVE_FAILED,
    "trx": "$ARTIFACTS_DIR/hand-live.trx"
  },
  "token_savings_status": "$TOKEN_SAVINGS_STATUS",
  "token_savings": {
    "count": $TOKEN_COUNT_JSON,
    "avg_percent": $TOKEN_AVG_JSON,
    "min_percent": $TOKEN_MIN_JSON,
    "max_percent": $TOKEN_MAX_JSON
  }
}
JSONEOF

    cat > "$ARTIFACTS_DIR/hand-codec-g-latest.md" <<MDEOF
# H.A.N.D. Codec G - Benchmark Report

- **Date:** $TIMESTAMP
- **Commit:** $COMMIT_SHA
- **.NET:** $DOTNET_VERSION
- **Mode:** $MODE
- **StrictCodecG:** true
- **Prompt purity:** verified by tests

## Results

| Suite | Status | Passed | Failed |
|-------|--------|--------|--------|
| Unit tests | passed | $UNIT_PASSED | $UNIT_FAILED |
| Cassette HandBenchmark + mutations | $CASSETTE_STATUS | $CASSETTE_PASSED | $CASSETTE_FAILED |
| LiveOllama | $LIVE_STATUS | $LIVE_PASSED | $LIVE_FAILED |

## Token Savings

Status: \`$TOKEN_SAVINGS_STATUS\`

| Count | Avg % | Min % | Max % |
|-------|-------|-------|-------|
| $TOKEN_COUNT_JSON | $TOKEN_AVG_JSON | $TOKEN_MIN_JSON | $TOKEN_MAX_JSON |

Token economy is parsed from runtime benchmark logs that measure L2/L3 memo wire
against expanded plaintext. If the current run did not emit those measurements,
the JSON report uses \`not_measured\` and \`null\` values.

## Artifacts

- Full report: [hand-codec-g.md](../../docs/benchmarks/hand-codec-g.md)
- Benchmark matrix: [benchmark-matrix.md](../../docs/benchmarks/benchmark-matrix.md)
- Cassette TRX: \`$ARTIFACTS_DIR/hand-benchmark.trx\`
- Mutation TRX: \`$ARTIFACTS_DIR/hand-benchmark-negative.trx\`
- Live TRX: \`$ARTIFACTS_DIR/hand-live.trx\`
MDEOF

    echo "   $ARTIFACTS_DIR/hand-codec-g-latest.json"
    echo "   $ARTIFACTS_DIR/hand-codec-g-latest.md"
    echo "   Done."
fi

echo ""
echo "== Benchmark complete =="
