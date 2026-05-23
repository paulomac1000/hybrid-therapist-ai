#!/usr/bin/env bash
# Build the hybrid-therapist Docker image locally.
# Uses Docker BuildKit named contexts so hand-codec stays an external dependency.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
HAND_CODEC_PATH="${HAND_CODEC_PATH:-/home/pablo/Projects/hand-codec}"
IMAGE_TAG="${IMAGE_TAG:-hybrid-therapist:local}"

if [ ! -d "$HAND_CODEC_PATH" ]; then
    echo "✗ hand-codec not found at $HAND_CODEC_PATH" >&2
    echo "  Set HAND_CODEC_PATH=/path/to/hand-codec or check out the repo." >&2
    exit 1
fi

echo "→ Building $IMAGE_TAG"
echo "  context:    $REPO_ROOT"
echo "  hand-codec: $HAND_CODEC_PATH"

cd "$REPO_ROOT"

DOCKER_BUILDKIT=1 docker buildx build \
    --build-context "hand-codec=$HAND_CODEC_PATH" \
    --load \
    -t "$IMAGE_TAG" \
    .

echo "✓ Built $IMAGE_TAG"
docker image inspect "$IMAGE_TAG" --format '  size: {{.Size}} bytes  ({{.RepoTags}})'
