#!/usr/bin/env bash
# Gate: has upstream published a version this fork has not released yet?
#
#   usage: upstream-version-gate.sh <upstream_tag> <our_tag>
#   stdout: "true"  -> build and publish a release
#           "false" -> nothing to do
#
# Why this exists: upstream tags plain ("2.1.0.0") and this fork tags with a
# leading v ("v2.1.0.0"). A raw string compare therefore never matched, so the
# 6-hourly schedule reported a new upstream version on every run and republished
# the same release four times a day.
set -euo pipefail

DOTTED='^[0-9]+(\.[0-9]+)*$'

normalize() {
  local t="${1:-}"
  t="${t#v}"
  t="${t#V}"
  printf '%s' "$t"
}

upstream=$(normalize "${1:-}")
ours=$(normalize "${2:-}")

# No upstream release to track.
if [ -z "$upstream" ]; then echo false; exit 0; fi

# Nothing of ours published yet -> build.
if [ -z "$ours" ]; then echo true; exit 0; fi

# Already released this exact version.
if [ "$upstream" = "$ours" ]; then echo false; exit 0; fi

# A tag that does not parse as a dotted version is not actionable. Skip rather
# than rebuild on every run for a tag the updater's Version.TryParse would reject.
if ! printf '%s' "$upstream" | grep -qE "$DOTTED"; then echo false; exit 0; fi
if ! printf '%s' "$ours" | grep -qE "$DOTTED"; then echo true; exit 0; fi

# Only a strictly newer upstream version is new. If this fork is ahead, there is
# nothing to do. sort -V orders dotted components as version numbers, so
# 2.10.0.0 sorts above 2.9.0.0 (a string sort would get that backwards).
newest=$(printf '%s\n%s\n' "$upstream" "$ours" | sort -V | tail -n1)
if [ "$newest" = "$ours" ]; then echo false; else echo true; fi
