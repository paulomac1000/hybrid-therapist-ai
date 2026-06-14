#!/usr/bin/env bash
set -euo pipefail
# Rebuild hybrid-therapist Docker image from local source and restart container.
# Run this after every .cs change to update the running container.

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "=== Building hybrid-therapist:local from local source ==="
docker build --no-cache -t hybrid-therapist:local "$REPO_ROOT"

echo "=== Restarting therapist container ==="
cd "$REPO_ROOT" && docker compose up -d therapist

echo "=== Done. Therapist container restarted with fresh image ==="
