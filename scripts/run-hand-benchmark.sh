#!/usr/bin/env bash
set -euo pipefail

# H.A.N.D. Benchmark Runner
#
# Usage:
#   ./scripts/run-hand-benchmark.sh                                     # cassette mode (default, compact, no Docker)
#   ./scripts/run-hand-benchmark.sh --variant semantic                  # semantic keys variant
#   ./scripts/run-hand-benchmark.sh --variant plaintext                 # natural language prose variant
#   ./scripts/run-hand-benchmark.sh --variant json                      # JSON variant
#   ./scripts/run-hand-benchmark.sh --checkpoints 0                     # test format learning strength
#   ./scripts/run-hand-benchmark.sh --all-variants --report             # run all variants and generate markdown/json reports
#   ./scripts/run-hand-benchmark.sh --live                              # live mode (requires Docker + Ollama)
#

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARTIFACTS_DIR="$REPO_ROOT/artifacts/benchmarks"
TIMESTAMP=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
COMMIT_SHA=$(git -C "$REPO_ROOT" rev-parse --short HEAD 2>/dev/null || echo "unknown")
DOTNET_VERSION=$(dotnet --version 2>/dev/null || echo "unknown")
REPORT_FLAG=false
MODE="cassette"
VARIANT="compact"
CHECKPOINTS="3"
ALL_VARIANTS=false

while [ $# -gt 0 ]; do
    case "$1" in
        --cassette) MODE="cassette"; shift ;;
        --live) MODE="live"; shift ;;
        --all) MODE="all"; shift ;;
        --report) REPORT_FLAG=true; shift ;;
        --variant)
            VARIANT="$2"
            shift 2
            ;;
        --variant=*)
            VARIANT="${1#*=}"
            shift
            ;;
        --checkpoints)
            CHECKPOINTS="$2"
            shift 2
            ;;
        --checkpoints=*)
            CHECKPOINTS="${1#*=}"
            shift
            ;;
        --all-variants)
            ALL_VARIANTS=true; shift ;;
        --help|-h)
            echo "Usage: $0 [--cassette|--live|--all] [--report] [--variant <compact|semantic|plaintext|json>] [--checkpoints <0|1|3|5>] [--all-variants]"
            echo "  --cassette      Cassette-only benchmark (no Docker/Ollama) - default"
            echo "  --live          Live benchmark (requires Docker Compose + Ollama)"
            echo "  --all           Run both cassette and live benchmarks"
            echo "  --report        Generate JSON + Markdown report artifacts"
            echo "  --variant       Benchmark variant: compact (default), semantic, plaintext, or json"
            echo "  --checkpoints   Number of priming checkpoints: 3 (default), 0, 1, 5"
            echo "  --all-variants  Run all variants (compact, semantic, plaintext, json, checkpoints) in sequence"
            exit 0
            ;;
        *)
            echo "Unknown argument: $1" >&2
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

echo "== H.A.N.D. Benchmark Runner =="
echo "  variant:   $VARIANT"
echo "  priming:   $CHECKPOINTS checkpoints"
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
    local trx_file="$1"
    local values
    values=$(grep -oP 'BENCHMARK_TOKEN_SAVINGS=\K[-0-9.]+' "$trx_file" 2>/dev/null || true)
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

run_single_variant() {
    local var="$1"
    local cp="$2"
    local filter="FullyQualifiedName~HandBenchmarkTests"
    local neg_filter="FullyQualifiedName~HandBenchmarkNegativeTests"
    local r_name="hand-compact"

    if [ "$var" = "semantic" ]; then
        filter="FullyQualifiedName~HandSemanticBenchmarkTests"
        neg_filter=""
        r_name="hand-semantic"
    elif [ "$var" = "plaintext" ]; then
        filter="FullyQualifiedName~HandPlaintextBenchmarkTests"
        neg_filter=""
        r_name="hand-plaintext"
    elif [ "$var" = "json" ]; then
        filter="FullyQualifiedName~HandJsonBenchmarkTests"
        neg_filter=""
        r_name="hand-json"
    fi

    echo "Running variant: $var ($cp checkpoints)..."
    export Models__HandWireVariant="$var"
    export Models__ImplicitPrimingCheckpointCount="$cp"

    set +e
    local test_res
    test_res=$(dotnet test "$REPO_ROOT/tests/HybridTherapist.Integration" -c Release --no-build --nologo \
        --filter "$filter" \
        --logger "trx;LogFileName=$ARTIFACTS_DIR/${r_name}-benchmark.trx" 2>&1)
    local test_exit=$?
    set -e

    local passed
    passed=$(extract_passed "$test_res")
    local failed
    failed=$(extract_failed "$test_res")
    if [ "$test_exit" -ne 0 ] && [ "$failed" -eq 0 ]; then failed=1; fi

    local neg_passed=0
    local neg_failed=0
    if [ -n "$neg_filter" ]; then
        echo "   Running negative tests for $var..."
        set +e
        local neg_res
        neg_res=$(dotnet test "$REPO_ROOT/tests/HybridTherapist.Integration" -c Release --no-build --nologo \
            --filter "$neg_filter" \
            --logger "trx;LogFileName=$ARTIFACTS_DIR/${r_name}-negative.trx" 2>&1)
        local neg_exit=$?
        set -e
        neg_passed=$(extract_passed "$neg_res")
        neg_failed=$(extract_failed "$neg_res")
        if [ "$neg_exit" -ne 0 ] && [ "$neg_failed" -eq 0 ]; then neg_failed=1; fi
        test_exit=$((test_exit + neg_exit))
    fi

    local total_passed=$((passed + neg_passed))
    local total_failed=$((failed + neg_failed))
    local status=$([ "$test_exit" -eq 0 ] && echo "passed" || echo "failed")

    parse_token_savings "$ARTIFACTS_DIR/${r_name}-benchmark.trx"

    echo "   Result: $status (Passed: $total_passed, Failed: $total_failed, Savings: ${TOKEN_SAVINGS_AVG:-N/A}%)"

    if [ "$REPORT_FLAG" = true ]; then
        local token_avg_json
        token_avg_json=$(json_number_or_null "$TOKEN_SAVINGS_AVG")
        local token_min_json
        token_min_json=$(json_number_or_null "$TOKEN_SAVINGS_MIN")
        local token_max_json
        token_max_json=$(json_number_or_null "$TOKEN_SAVINGS_MAX")
        cat > "$ARTIFACTS_DIR/${r_name}-latest.json" <<JSONEOF
{
  "variant": "$var",
  "timestamp_utc": "$TIMESTAMP",
  "commit": "$COMMIT_SHA",
  "checkpoints": $cp,
  "status": "$status",
  "passed": $total_passed,
  "failed": $total_failed,
  "token_savings": {
    "avg_percent": $token_avg_json,
    "min_percent": $token_min_json,
    "max_percent": $token_max_json
  }
}
JSONEOF

        cat > "$ARTIFACTS_DIR/${r_name}-latest.md" <<MDEOF
# H.A.N.D. ${var^} Variant - Report

- **Date:** $TIMESTAMP
- **Commit:** $COMMIT_SHA
- **Checkpoints:** $cp
- **Status:** $status
- **Passed:** $total_passed
- **Failed:** $total_failed
- **Average Token Savings:** ${TOKEN_SAVINGS_AVG:-not measured}%
MDEOF
    fi

    return $test_exit
}

GLOBAL_EXIT=0

if [ "$ALL_VARIANTS" = true ]; then
    echo "5. Running all matrix variants sequentially..."
    
    # Run Compact (e7, s9)
    run_single_variant "compact" 3 || GLOBAL_EXIT=1
    
    # Run Semantic (em, sv)
    run_single_variant "semantic" 3 || GLOBAL_EXIT=1
    
    # Run Plaintext
    run_single_variant "plaintext" 3 || GLOBAL_EXIT=1
    
    # Run JSON
    run_single_variant "json" 3 || GLOBAL_EXIT=1
    
    # Run Checkpoint count experiment tests
    echo "Running Checkpoint count experiment tests..."
    set +e
    dotnet test "$REPO_ROOT/tests/HybridTherapist.Integration" -c Release --no-build --nologo \
        --filter "FullyQualifiedName~HandCheckpointCountBenchmarkTests" \
        --logger "trx;LogFileName=$ARTIFACTS_DIR/checkpoints-experiment.trx"
    cp_exit=$?
    set -e
    GLOBAL_EXIT=$((GLOBAL_EXIT + cp_exit))
else
    if [ "$RUN_CASSETTE" = true ]; then
        run_single_variant "$VARIANT" "$CHECKPOINTS" || GLOBAL_EXIT=1
    fi
fi

# Run live integration tests if --live
if [ "$RUN_LIVE" = true ]; then
    echo "6. Running live H.A.N.D. benchmark..."
    export Models__HandWireVariant="$VARIANT"
    export Models__ImplicitPrimingCheckpointCount="$CHECKPOINTS"
    set +e
    LIVE_RESULT=$(OLLAMA_HOST=http://localhost:11434 dotnet test "$REPO_ROOT/tests/HybridTherapist.Integration" -c Release --no-build --nologo \
        --filter "LiveOllama" \
        --logger "trx;LogFileName=$ARTIFACTS_DIR/hand-live.trx" 2>&1)
    LIVE_EXIT=$?
    set -e
    LIVE_PASSED=$(extract_passed "$LIVE_RESULT")
    LIVE_FAILED=$(extract_failed "$LIVE_RESULT")
    if [ "$LIVE_EXIT" -ne 0 ] && [ "$LIVE_FAILED" -eq 0 ]; then LIVE_FAILED=1; fi
    echo "   Passed: $LIVE_PASSED  Failed: $LIVE_FAILED"
    GLOBAL_EXIT=$((GLOBAL_EXIT + LIVE_EXIT))
fi

if [ "$GLOBAL_EXIT" -ne 0 ]; then
    echo ""
    echo "BENCHMARK MATRIX FAILED"
    exit 1
fi

echo ""
echo "BENCHMARK MATRIX PASSED"
exit 0
