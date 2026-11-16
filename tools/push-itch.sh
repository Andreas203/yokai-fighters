#!/usr/bin/env bash
# Push the Windows build to itch.io with butler (https://itch.io/docs/butler/): shnabin/yokai-fighters, channel "windows".
#   tools/push-itch.sh [--export] [--dry-run]
#     --export   run tools/export-windows.sh first (otherwise build/windows/ must already exist)
#     --dry-run  show what would be pushed; uploads nothing
# Needs butler on PATH (or BUTLER=/path/to/butler) and a login: run `butler login` once, or set BUTLER_API_KEY (CI).
# ITCH_TARGET overrides user/game:channel; ITCH_VERSION overrides the version label (default: git describe).
# butler uploads only the files that changed since the last push, and itch.io's app updates players by the same patch.
set -euo pipefail
REPO="$(cd "$(dirname "$0")/.." && pwd)"
TARGET="${ITCH_TARGET:-shnabin/yokai-fighters:windows}"
BUTLER="${BUTLER:-butler}"
OUT="$REPO/build/windows"

export_first=0; dry=0
for a in "$@"; do
  case "$a" in
    --export) export_first=1 ;;
    --dry-run) dry=1 ;;
    *) echo "usage: tools/push-itch.sh [--export] [--dry-run]" >&2; exit 2 ;;
  esac
done

command -v "$BUTLER" >/dev/null || { echo "butler not found: install it (https://itch.io/docs/butler/) or set BUTLER" >&2; exit 1; }
if [ "$export_first" = 1 ]; then bash "$REPO/tools/export-windows.sh"; fi
[ -f "$OUT/YokaiFighters.exe" ] || { echo "no build in $OUT: run with --export, or tools/export-windows.sh" >&2; exit 1; }

VERSION="${ITCH_VERSION:-$(git -C "$REPO" describe --tags --always --dirty)}"
args=(push "$OUT" "$TARGET" --userversion "$VERSION" --if-changed)
if [ "$dry" = 1 ]; then args+=(--dry-run); fi
echo "butler ${args[*]}"
"$BUTLER" "${args[@]}"
if [ "$dry" = 0 ]; then "$BUTLER" status "$TARGET" || true; fi
