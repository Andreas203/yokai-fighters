# rig_check.py test fixture: Meshy humanoid bone list

Test input for `tools/blender/rig_check.py`, dumped from Ryo's accepted rig
(`game/assets/generated/characters/ryo/ryo-rigged.glb`) with `--dump-skeleton`.
It shows the `skeleton-json` block format. The shared skeleton itself lives in
`assets/specs/rigs/skeleton.md` (YOK-111); if the two disagree, skeleton.md wins.

```skeleton-json
{
 "skeleton": "meshy-humanoid-24",
 "height_m": 1.75,
 "up_axis": "Y",
 "rest_pose": "T",
 "bones": [
  {"name": "Hips", "parent": null},
  {"name": "Spine02", "parent": "Hips"},
  {"name": "Spine01", "parent": "Spine02"},
  {"name": "Spine", "parent": "Spine01"},
  {"name": "neck", "parent": "Spine"},
  {"name": "Head", "parent": "neck"},
  {"name": "head_end", "parent": "Head"},
  {"name": "headfront", "parent": "Head"},
  {"name": "LeftShoulder", "parent": "Spine"},
  {"name": "LeftArm", "parent": "LeftShoulder"},
  {"name": "LeftForeArm", "parent": "LeftArm"},
  {"name": "LeftHand", "parent": "LeftForeArm"},
  {"name": "RightShoulder", "parent": "Spine"},
  {"name": "RightArm", "parent": "RightShoulder"},
  {"name": "RightForeArm", "parent": "RightArm"},
  {"name": "RightHand", "parent": "RightForeArm"},
  {"name": "LeftUpLeg", "parent": "Hips"},
  {"name": "LeftLeg", "parent": "LeftUpLeg"},
  {"name": "LeftFoot", "parent": "LeftLeg"},
  {"name": "LeftToeBase", "parent": "LeftFoot"},
  {"name": "RightUpLeg", "parent": "Hips"},
  {"name": "RightLeg", "parent": "RightUpLeg"},
  {"name": "RightFoot", "parent": "RightLeg"},
  {"name": "RightToeBase", "parent": "RightFoot"}
 ],
 "bone_maps": {}
}
```
