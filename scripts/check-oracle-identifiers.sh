#!/usr/bin/env bash
set -euo pipefail

root="${1:-.}"
max_len="${ORACLE_IDENTIFIER_MAX:-30}"
status=0

while IFS= read -r file; do
  awk -v file="$file" -v max_len="$max_len" '
    BEGIN {
      in_string = 0
    }
    {
      line = $0
      scrubbed = ""
      for (i = 1; i <= length(line); i++) {
        char = substr(line, i, 1)
        next_char = substr(line, i + 1, 1)
        if (char == "'"'"'") {
          in_string = !in_string
          scrubbed = scrubbed " "
        } else if (!in_string && char == "-" && next_char == "-") {
          break
        } else if (in_string) {
          scrubbed = scrubbed " "
        } else {
          scrubbed = scrubbed char
        }
      }

      count = split(scrubbed, parts, /[^A-Za-z0-9_]+/)
      for (i = 1; i <= count; i++) {
        token = parts[i]
        if (token ~ /[A-Za-z_]/ && length(token) > max_len) {
          printf "%s:%d:%d:%s\n", file, FNR, length(token), token
          found = 1
        }
      }
    }
    END {
      if (found) {
        exit 1
      }
    }
  ' "$file" || status=1
done < <(
  if [ -f "$root" ]; then
    printf '%s\n' "$root"
  else
    find "$root" -type f -name '*.sql' -print
  fi
)

exit "$status"
