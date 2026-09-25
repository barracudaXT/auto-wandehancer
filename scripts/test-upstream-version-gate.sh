#!/usr/bin/env bash
# Runnable check for scripts/upstream-version-gate.sh.
# The gate decides whether CI publishes a release. Wrong in one direction it
# republishes the same release forever; wrong in the other it never ships an
# upstream update. Both are silent, so assert every case.
set -euo pipefail
cd "$(dirname "$0")"
GATE=./upstream-version-gate.sh

fails=0
check() {
  local expect="$1" up="$2" ours="$3" label="$4"
  local got
  got=$(bash "$GATE" "$up" "$ours")
  if [ "$got" = "$expect" ]; then
    printf '  ok   %-38s -> %s\n' "$label" "$got"
  else
    printf '  FAIL %-38s -> %s (expected %s)\n' "$label" "$got" "$expect"
    fails=$((fails + 1))
  fi
}

echo "upstream-version-gate:"
check false '2.1.0.0' 'v2.1.0.0' 'same version, v-prefix mismatch'
check false 'v2.1.0.0' 'v2.1.0.0' 'same version, both prefixed'
check false '2.1.0.0' '2.1.0.0'   'same version, both plain'
check true  '2.2.0.0' 'v2.1.0.0'  'upstream newer'
check true  '2.10.0.0' 'v2.9.0.0' 'upstream newer (numeric, not string)'
check false '2.1.0.0' 'v2.2.0.0'  'fork ahead'
check false '' 'v2.1.0.0'         'no upstream release'
check true  '2.1.0.0' ''          'nothing released by us yet'
check false 'release-latest' 'v2.1.0.0' 'unparseable upstream tag'
check true  '2.1.0.0' 'vNext'     'unparseable our tag'

if [ "$fails" -ne 0 ]; then
  echo "FAILED: $fails case(s)"
  exit 1
fi
echo "all passed"
