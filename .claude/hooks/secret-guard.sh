#!/usr/bin/env bash

# PreToolUse guard: deny writes containing common credential-shaped strings.
# Exit 2 tells Claude Code to deny the write. Infrastructure configuration must be injected.

set -euo pipefail

payload="$(cat)"

# Intentionally heuristic. Keep this focused on high-signal secret shapes to reduce false positives.
patterns='(sk-[A-Za-z0-9_-]{16,}|AKIA[0-9A-Z]{16}|-----BEGIN [A-Z ]*PRIVATE KEY-----|SharedAccessKey[[:space:]]*=[[:space:]]*[^;[:space:]]+|AccountKey[[:space:]]*=[[:space:]]*[^;[:space:]]+|Password[[:space:]]*=[[:space:]]*[^;[:space:]]{6,}|User ID[[:space:]]*=[^;]+;[^\n]*(Password|Pwd)[[:space:]]*=|Server[[:space:]]*=[^;]+;[^\n]*(Password|Pwd)[[:space:]]*=|Data Source[[:space:]]*=[^;]+;[^\n]*(Password|Pwd)[[:space:]]*=|Endpoint=sb://[^;]+;[^\n]*SharedAccessKey)'

if printf '%s' "$payload" | grep -Eiq "$patterns"; then
  echo "secret-guard: blocked a write containing a credential-shaped value. Use Aspire/Azure/user-secret/environment configuration, not literals." >&2
  exit 2
fi

exit 0
