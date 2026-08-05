#!/usr/bin/env bash
set -euo pipefail
payload="$(cat)"
# Conservative guard for credential-shaped content in Claude Code Write/Edit payloads.
pattern='(password|passwd|pwd|client[_-]?secret|api[_-]?key|access[_-]?token|connectionstrings?)[[:space:]"'"']*[:=][[:space:]"'"']*[^${[:space:]"'"']{8,}'
if printf '%s' "$payload" | grep -Eiq "$pattern"; then
  echo "Blocked: credential-shaped literal detected. Use configuration/user-secrets/environment variables instead." >&2
  exit 2
fi
exit 0
