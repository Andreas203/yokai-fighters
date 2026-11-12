#!/bin/sh
# YOK-trailer: records every trailer shot with Godot Movie Maker (1080p60) into trailer/raw/. Usage: sh trailer/record.sh [name...]
G="C:/Users/Andreas/Downloads/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
ROOT="C:/Users/Andreas/projects/wf-trailer"
ONLY="$*"
shot() { name=$1; shift
  [ -n "$ONLY" ] && ! echo " $ONLY " | grep -q " $name " && return
  "$G" --audio-driver Dummy --path "$ROOT/game" --write-movie "$ROOT/trailer/raw/$name.avi" --fixed-fps 60 --resolution 1920x1080 \
    res://tests/captures/trailer_capture.tscn -- "$@" > "$ROOT/trailer/raw/$name.log" 2>&1; echo "$name: $(grep 'recording from' "$ROOT/trailer/raw/$name.log")"; }

shot kitsune-clash-2  shot=fight opp=kitsune seed=2 ff=1380 len=420
shot kitsune-clash-1  shot=fight opp=kitsune seed=1 ff=1330 len=360
shot kitsune-fox-3    shot=fight opp=kitsune seed=3 ff=130 len=360
shot oni-quake-4      shot=fight opp=oni elder=1 stage=ShrineGate seed=4 ff=140 len=360
shot oni-quake-3      shot=fight opp=oni elder=1 stage=ShrineGate seed=3 ff=90 len=360
shot oni-quake-4b     shot=fight opp=oni elder=1 stage=ShrineGate seed=4 ff=1140 len=300
shot kappa-spin-3     shot=fight opp=kappa elder=1 stage=Riverbank seed=3 ff=580 len=300
shot kappa-grab-2     shot=fight opp=kappa elder=1 stage=Riverbank seed=2 ff=800 len=200
shot kappa-grab-4     shot=fight opp=kappa seed=4 ff=600 len=240
shot combo-kd-k1      shot=fight opp=kitsune seed=1 ff=1840 len=200
shot combo-kd-oni2    shot=fight opp=oni elder=1 stage=ShrineGate seed=2 ff=1620 len=200
shot guard-k1         shot=fight opp=kitsune seed=1 ff=620 len=200
shot ko-kappa-3       shot=fight opp=kappa seed=3 ff=2200 len=120 ko=1
shot ko-oni-3         shot=fight opp=oni seed=3 ff=1900 len=120 ko=1
shot ko-kitsune-1     shot=fight opp=kitsune seed=1 ff=3000 len=120 ko=1
shot tanuki-mirror-4  shot=fight opp=tanuki copy=foxfire seed=4 ff=990 len=300
shot tanuki-fox-2     shot=fight opp=tanuki copy=foxfire seed=2 ff=1240 len=300
shot tanuki-reveal    shot=reveal opp=tanuki copy=foxfire seed=4 len=240
shot map              shot=map seed=5 len=240
shot reward           shot=reward len=210
# Re-cut 2: the drafting beat (real reward drafts) and Ryo using each drafted special (loadout via give=)
shot draft-foxfire   shot=draft opp=kitsune want=foxfire seed=1 len=200
shot draft-club      shot=draft opp=oni want=iron-club-charge seed=1 len=240 fd=110
shot draft-grab      shot=draft opp=kappa want=river-grab seed=1 len=200
shot use-foxfire     shot=fight opp=oni seed=3 give=foxfire ff=300 len=150
shot use-club-kappa  shot=fight opp=kappa seed=3 give=iron-club-charge ff=630 len=130
shot use-grab        shot=fight opp=kappa seed=4 give=river-grab ff=40 len=130
shot use-grab-oni    shot=fight opp=oni seed=3 give=river-grab ff=1895 len=130
