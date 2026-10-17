# Ryo cloth regions (rig_check.py --cloth)

Owner: rig-specialist. Ticket: YOK-110. Designer decision on YOK-110: cloth verts are exempt from the stress-pose edge-tear and volume checks, and their stretch is still reported as info.

**Ryo has no cloth region.** The take-2 model (`assets/specs/ryo.md`) has no long coat tail, and the coat ends at the sash with no free hem.

The cloth mask audit showed that the only verts a coat-hem rule could pick (`UpLeg`-dominant, beyond 0.135 m, from 0.80 to 0.98 m) are the lightning-shaped paper charm and pouch at the front of the hip. Those are props:
- The rig plan bakes the sash, paper and pouch rigid to `Hips`.
- Today they are weighted to the thighs instead. That is a weight fault for the repair pass, not cloth, so they stay in the checks.

If a later take adds the long coat tail, it gets extra bones (`X_CoatTail_<n>`, skeleton.md) and a region here.

```cloth-regions-json
{"fighter": "ryo", "regions": []}
```
