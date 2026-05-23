#!/bin/sh
# Pull Ollama models declared under `models:` in stack.yaml.
# Cortexa parity: same input file format, same provider semantics.
# Skips entries with provider != ollama (cloud models pull on demand).
set -u

OLLAMA_HOST="${OLLAMA_HOST:-http://ollama:11434}"
STACK_YAML="${STACK_YAML:-/config/stack.yaml}"
SKIP="${SKIP_MODEL_LOADS:-false}"

log()  { echo "📦 [LOADER] $(date '+%H:%M:%S') $*"; }
ok()   { echo "✅ [LOADER] $(date '+%H:%M:%S') $*"; }
warn() { echo "⚠️  [LOADER] $(date '+%H:%M:%S') $*"; }
err()  { echo "❌ [LOADER] $(date '+%H:%M:%S') $*"; }

if [ "$SKIP" = "true" ] || [ "$SKIP" = "1" ]; then
    warn "SKIP_MODEL_LOADS=true — exiting without pulling"
    exit 0
fi

# Alpine baseline doesn't ship jq/yq/curl — install on first run
if ! command -v curl >/dev/null 2>&1 || ! command -v jq >/dev/null 2>&1; then
    log "Installing curl, jq, yq"
    apk add --no-cache curl jq yq >/dev/null
fi

if [ ! -f "$STACK_YAML" ]; then
    err "stack.yaml not found at $STACK_YAML"
    exit 1
fi

log "Waiting for Ollama API at $OLLAMA_HOST..."
for i in $(seq 1 60); do
    if curl -sf "${OLLAMA_HOST}/api/tags" >/dev/null 2>&1; then
        ok "Ollama API ready"
        break
    fi
    sleep 2
done

model_exists() {
    name="$1"
    base="${name%%:*}"
    curl -sf "${OLLAMA_HOST}/api/tags" 2>/dev/null \
        | jq -e ".models[] | select(.name | startswith(\"${base}\"))" >/dev/null 2>&1
}

pull_model() {
    name="$1"
    if model_exists "$name"; then
        ok "'$name' already present"
        return 0
    fi
    log "Pulling: $name (may take several minutes)"
    curl -sf -X POST "${OLLAMA_HOST}/api/pull" \
        -H "Content-Type: application/json" \
        -d "{\"name\": \"${name}\", \"stream\": false}" \
        --max-time 3600 >/dev/null 2>&1
    sleep 3
    if model_exists "$name"; then
        ok "'$name' pulled"
        return 0
    fi
    warn "Failed to verify pull of '$name'"
    return 1
}

KEYS=$(yq -r '.models | keys | .[]' "$STACK_YAML")
LOADED=0
SKIPPED=0
FAILED=0

for KEY in $KEYS; do
    NAME=$(yq -r ".models.\"$KEY\".name" "$STACK_YAML")
    PROVIDER=$(yq -r ".models.\"$KEY\".provider // \"ollama\"" "$STACK_YAML")

    log ""
    log "━━━ $KEY ($PROVIDER) → $NAME"

    if [ "$PROVIDER" != "ollama" ] && [ "$PROVIDER" != "null" ]; then
        log "  skipping cloud-only model"
        SKIPPED=$((SKIPPED + 1))
        continue
    fi

    if pull_model "$NAME"; then
        LOADED=$((LOADED + 1))
    else
        FAILED=$((FAILED + 1))
    fi
done

log ""
log "Summary: $LOADED pulled, $SKIPPED cloud-only skipped, $FAILED failed"
log "Available models:"
curl -sf "${OLLAMA_HOST}/api/tags" | jq -r '.models[] | "  • \(.name)"' 2>/dev/null || true

# Never block startup — therapist degrades gracefully if a model is missing
exit 0
