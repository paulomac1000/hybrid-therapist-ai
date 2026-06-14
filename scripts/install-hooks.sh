#!/usr/bin/env bash
# Install/Uninstall git hooks for hybrid-therapist
set -euo pipefail

HOOKS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/.githooks"
CMD="${1:-install}"

case "$CMD" in
    install)
        echo "Installing hooks from $HOOKS_DIR"
        chmod +x "$HOOKS_DIR"/*
        git config core.hooksPath "$HOOKS_DIR"
        echo "✅ Hooks installed. Current hooksPath: $(git config core.hooksPath)"
        echo ""
        echo "Available hooks:"
        for f in "$HOOKS_DIR"/*; do
            echo "  ├ $(basename "$f")"
        done
        ;;
    uninstall)
        echo "Uninstalling hooks"
        git config --unset core.hooksPath
        echo "✅ Hooks uninstalled. Restored to default (.git/hooks/)"
        ;;
    *)
        echo "Usage: $0 [install|uninstall]"
        echo "  install   (default) Install .githooks as the hooks path"
        echo "  uninstall           Remove the hooks path setting"
        exit 1
        ;;
esac
