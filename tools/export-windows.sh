#!/usr/bin/env bash
# YOK-39: playable Windows build of the demo.
#   GODOT=/path/to/Godot_v4.7.2-stable_mono_win64_console.exe tools/export-windows.sh
# Needs the 4.7.2.stable.mono export templates installed. Output: build/windows/ and build/YokaiFighters-Demo-Windows.zip.
# The game reads its JSON content with System.IO, so data/ (and the TEST FIXTURE folders the kits still fall back
# to, e.g. Kitsune's air normals) are copied next to the exe, where ContentPaths looks in an exported build.
set -euo pipefail
REPO="$(cd "$(dirname "$0")/.." && pwd)"
GODOT="${GODOT:-C:/Users/Andreas/Downloads/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe}"
OUT="$REPO/build/windows"

rm -rf "$OUT" "$REPO/build/YokaiFighters-Demo-Windows.zip"
mkdir -p "$OUT"
(cd "$REPO/game" && dotnet build -c ExportRelease)
"$GODOT" --headless --path "$REPO/game" --export-release "Windows Desktop" ../build/windows/YokaiFighters.exe

mkdir -p "$OUT/data" "$OUT/fixtures"
for d in clips modifiers moves profiles story; do cp -r "$REPO/data/$d" "$OUT/data/$d"; done
for d in ryo-normals ryo-air-normals throws specials rewards; do cp -r "$REPO/game/tests/fixtures/$d" "$OUT/fixtures/$d"; done

powershell.exe -NoProfile -Command "Compress-Archive -Path '$(cygpath -w "$OUT")\\*' -DestinationPath '$(cygpath -w "$REPO/build")\\YokaiFighters-Demo-Windows.zip' -Force"
ls -l "$REPO/build/YokaiFighters-Demo-Windows.zip"
