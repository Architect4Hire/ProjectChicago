#!/usr/bin/env bash
set -euo pipefail
payload="$(cat)"
file="$(printf '%s' "$payload" | sed -nE 's/.*"file_path"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/p' | head -1)"
[ -n "$file" ] || exit 0
[ -f "$file" ] || exit 0
case "$file" in
  *.cs)
    command -v dotnet >/dev/null 2>&1 && dotnet format whitespace --include "$file" --no-restore >/dev/null 2>&1 || true
    ;;
  *.ts|*.html|*.scss|*.css|*.json|*.md)
    if command -v npx >/dev/null 2>&1 && [ -f package.json ]; then
      npx prettier --write "$file" >/dev/null 2>&1 || true
    fi
    ;;
esac
exit 0
