#!/bin/sh
# YOK-finish-qa: measure (and with render=1 shoot) every body the fight shows, one Godot run per body.
# Usage: tools/rig_qa/run.sh <out dir> [render=0|1] [bodies...]
OUT="$1"; R="${2:-render=1}"; shift 2 2>/dev/null
BODIES="${*:-ryo kitsune oni kappa tanuki}"
GODOT="${GODOT:-C:/Users/Andreas/Downloads/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe}"
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
mkdir -p "$OUT"
for b in $BODIES; do
  "$GODOT" --path "$ROOT/game" res://tests/captures/finish_qa_capture.tscn --audio-driver Dummy -- out="$OUT" bodies="$b" "$R" $QA_EXTRA 2>&1 | grep -E "^$b: height|wrote|not playable"
done
