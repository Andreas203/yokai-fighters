#!/bin/sh
# YOK-finish-qa: foot lock (tools/blender/foot_lock.py) on the clips whose planted foot slides in the game's
# root-stripped presentation. In place; logs to assets/specs/rigs/logs/foot-lock/. Start = data/clips trim_start.
BL="${BLENDER:-/c/Program Files/Blender Foundation/Blender 4.1/blender.exe}"
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
G="$ROOT/game/assets/generated/clips"
LOG="$ROOT/assets/specs/rigs/logs/foot-lock"
mkdir -p "$LOG"
while read -r rel start extra; do
  [ -z "$rel" ] && continue
  name=$(echo "$rel" | tr '/' '_' | sed 's/\.glb$//')
  "$BL" -b --python "$ROOT/tools/blender/foot_lock.py" -- --clip "$G/$rel" --out "$G/$rel" --start "$start" $extra --log "$LOG/$name.json" 2>&1 | grep -E "^foot_lock|Error|Traceback" | cut -c1-220
done <<LIST
ryo-tripo/ryo-hk.glb 46
ryo-tripo/ryo-foxfire.glb 32
ryo-tripo/ryo-grab-2.glb 30
ryo-tripo/ryo-lk.glb 44
ryo-tripo/ryo-mk.glb 33
ryo-tripo/ryo-heavy.glb 28
ryo-tripo/ryo-hit-high.glb 28
ryo-tripo/ryo-hit-low.glb 8
ryo/ryo-hk.glb 46
ryo-grab-2.glb 30
kitsune/kitsune-foxfire.glb 31
kitsune/kitsune-hk.glb 46
kitsune/kitsune-throw.glb 30
kitsune/kitsune-mk.glb 31
kitsune/kitsune-hit-high.glb 28
kitsune/kitsune-hit-low.glb 10
kitsune-heavy.glb 28
oni/oni-heavy-club.glb 0
oni/oni-swing-club.glb 0
LIST
