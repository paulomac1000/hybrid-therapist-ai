#!/bin/bash
set -euo pipefail

INPUT_PATH="${1:?Usage: $0 <path>}"
OUTPUT_DIR="."
SCRIPT_NAME="dump-to-md.sh"

if [ ! -d "$INPUT_PATH" ]; then
    echo "Error: not a directory" >&2
    exit 1
fi

ABS_PATH=$(realpath "$INPUT_PATH")
FOLDER_NAME=$(basename "$ABS_PATH")
OUTPUT_FILE="${OUTPUT_DIR}/${FOLDER_NAME}.md"
OUTPUT_NAME=$(basename "$OUTPUT_FILE")

if git -C "$ABS_PATH" rev-parse --git-dir &>/dev/null; then
    FILES=$(git -C "$ABS_PATH" ls-files --cached --others --exclude-standard)
elif [ -f "${ABS_PATH}/.gitignore" ]; then
    FILES=$(find "$ABS_PATH" -type f -printf '%P\n' 2>/dev/null | grep -v -f <(sed 's|^/||;s|/$||;/^[ 	]*$/d;/\*/s|\*|.*|g' "${ABS_PATH}/.gitignore") 2>/dev/null || find "$ABS_PATH" -type f -printf '%P\n')
else
    FILES=$(find "$ABS_PATH" -type f -printf '%P\n')
fi

echo "# $FOLDER_NAME" > "$OUTPUT_FILE"
echo "" >> "$OUTPUT_FILE"

if [ -z "${FILES:-}" ]; then
    echo "Warning: no files found" >&2
fi

while IFS= read -r f; do
    [ -z "$f" ] && continue
    [ "$f" = "$SCRIPT_NAME" ] || [ "$f" = "$OUTPUT_NAME" ] && continue
    full="$ABS_PATH/$f"
    [ -f "$full" ] || continue
    echo "## $f" >> "$OUTPUT_FILE"
    echo '```' >> "$OUTPUT_FILE"
    cat "$full" >> "$OUTPUT_FILE"
    echo >> "$OUTPUT_FILE"
    echo '```' >> "$OUTPUT_FILE"
    echo "" >> "$OUTPUT_FILE"
done <<< "$FILES"

echo "Done: $OUTPUT_FILE"
