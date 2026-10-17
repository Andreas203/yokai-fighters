# Oni cloth regions (rig_check.py --cloth)

Owner: rig-specialist. Ticket: YOK-112. The YOK-110 designer rule applies: cloth verts are exempt from the stress-pose edge-tear and volume checks, and their stretch is still reported as info. Same rule as `cloth/kitsune.md`: a vert is cloth when its highest-weight bone is a region bone and, at rest, it sits inside the region's limits. No vertex indices. `Hips` and the spine and head chain are never cloth.

Frame: the game file `oni-rigged-220.glb` (2.2 m; Blender world metres, +Z up).

- **trousers**: the baggy indigo trousers are one shell with no legs modelled under them, like Kitsune's hakama. Every `UpLeg`/`Leg` vert from 0.62 m (just above the hem, where the shin wraps start) to 1.10 m (below the rope belt; the hip joints are at 1.128 m) is trouser cloth.
  - Both ends stay checked: the hem and the shin wraps below 0.62 m, and the waist, belt and seat band above 1.10 m, where the trousers meet `Hips`.

```cloth-regions-json
{
 "fighter": "oni",
 "regions": [
  {"name": "trousers", "bones": ["LeftUpLeg", "RightUpLeg", "LeftLeg", "RightLeg"],
   "min_radius_m": 0.0, "z_band_m": [0.62, 1.10]}
 ]
}
```
