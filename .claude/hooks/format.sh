#!/usr/bin/env bash

# PostToolUse hook: format the file Claude just edited.
# Formatting failures do not block an edit, but a missing frontend formatter is reported.

set -euo pipefail

input="$(cat)"

if command -v jq >/dev/null 2>&1; then
  file="$(printf '%s' "$input" | jq -r '.tool_input.file_path // empty')"
else
  file="$(printf '%s' "$input" \
    | grep -o '"file_path"[[:space:]]*:[[:space:]]*"[^"]*"' \
    | head -1 \
    | sed 's/.*"file_path"[[:space:]]*:[[:space:]]*"//; s/"$//')"
fi

[ -z "${file:-}" ] && exit 0
[ -f "$file" ] || exit 0

repo_root="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"

case "$file" in
  *.cs)
    if command -v dotnet >/dev/null 2>&1; then
      dotnet format --include "$file" >/dev/null 2>&1 || true
    fi
    ;;

  *.ts|*.tsx|*.js|*.jsx|*.css|*.scss|*.json|*.md)
    # Use the repository's pinned frontend formatter. Do not let npx download a floating version.
    prettier="$repo_root/src/web/node_modules/.bin/prettier"
    if [ -x "$prettier" ]; then
      "$prettier" --write "$file" >/dev/null 2>&1 || true
    else
      echo "format.sh: frontend prettier not installed; skipped $file (install src/web dependencies first)" >&2
    fi
    ;;
esac

exit 0
