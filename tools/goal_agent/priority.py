"""Decides what to build next.

`priority_score()` turns one unbuilt feature into a number and the reasons for it.
Every input is a fact read from the design (section G of rules.md) or from the scan:

  never cut   G1 lists what the game cannot ship without. Those come first.
  schedule    G5 gives each milestone a week. Work due this week or already late
              outranks work due later.
  unblocks    how many other unbuilt features wait on this one. Building the thing
              everything else needs removes the most gaps per write.
  cut risk    G2 and G3 list what is cut if the build falls behind, in order. Work
              that may be cut is built last, the first cut last of all.
  size        smaller work first when the rest is equal: the designer reviews every
              merge, and a small change is reviewed sooner.
  started     a feature the scan finds half-built is cheaper to finish than to start.

A feature whose prerequisites are not built is not ranked at all: it is blocked, and
the reason it is blocked is what raises the score of the feature it waits on.
"""
from __future__ import annotations

from dataclasses import dataclass, field

WEIGHTS = {
    "never_cut": 40,       # flat: outweighs everything except being blocked
    "schedule": 10,        # x urgency 0-3
    "unblocks": 6,         # x waiting features, at most UNBLOCKS_CAP of them
    "cut_risk": -4,        # x (how early it is cut: 5 for the first cut ... 1 for the last)
    "size": -5,            # x size 1-3
    "started": 5,          # flat, when partial
}
UNBLOCKS_CAP = 5


@dataclass
class Score:
    feature_id: str
    total: int = 0
    parts: list[tuple[str, int, str]] = field(default_factory=list)   # (factor, points, why)
    blocked_by: list[str] = field(default_factory=list)
    route: str = ""        # why the agent cannot write it itself, when it cannot

    @property
    def eligible(self) -> bool:
        return not self.blocked_by and not self.route

    def add(self, factor: str, points: int, why: str) -> None:
        self.parts.append((factor, points, why))
        self.total += points


def waiting_on(feature_id: str, gaps: dict) -> list[str]:
    """Unbuilt features that need this one, directly or through another unbuilt feature."""
    found: list[str] = []
    frontier = [feature_id]
    while frontier:
        cur = frontier.pop()
        for g in gaps.values():
            if g.status != "present" and cur in g.feature.get("needs", []) and g.id not in found:
                found.append(g.id)
                frontier.append(g.id)
    return sorted(found)


def urgency(week: int, current_week: int) -> int:
    """3 = due this week or late, 2 = due next week, 1 = the week after, 0 = later."""
    return max(0, 3 - max(0, week - current_week))


def priority_score(feature: dict, design, gaps: dict, current_week: int) -> Score:
    """The score of one unbuilt feature, with the reason for every point."""
    s = Score(feature["id"])
    gap = gaps[feature["id"]]

    s.blocked_by = [n for n in feature.get("needs", []) if gaps[n].status != "present"]
    if feature["kind"] != "code":
        s.route = f"{feature['kind']} work: goes to {feature['owner']} through the producer"
    elif "build" not in feature:
        s.route = f"no build brief yet: goes to {feature['owner']} through the producer"

    if entry := design.is_never_cut(feature["terms"]):
        s.add("never cut", WEIGHTS["never_cut"], f"G1 lists \"{entry}\"")

    week = design.week_of(feature["milestone"])
    u = urgency(week, current_week)
    when = "due this week or late" if u == 3 else f"due in week {week}"
    s.add("schedule", WEIGHTS["schedule"] * u, f"G5 puts \"{feature['milestone']}\" in week {week}; now week {current_week}: {when}")

    waiting = waiting_on(feature["id"], gaps)
    if waiting:
        n = min(len(waiting), UNBLOCKS_CAP)
        s.add("unblocks", WEIGHTS["unblocks"] * n, f"{len(waiting)} unbuilt feature(s) wait on it: {', '.join(waiting)}")

    rank = design.cut_rank(feature["terms"])
    if rank is not None:
        early = len(design.cut_order) - rank
        s.add("cut risk", WEIGHTS["cut_risk"] * early,
              f"\"{design.cut_order[rank]}\" is cut {rank + 1} of {len(design.cut_order)} if the build falls behind (G2, G3)")

    s.add("size", WEIGHTS["size"] * feature["size"], f"size {feature['size']} of 3")

    if gap.status == "partial":
        s.add("started", WEIGHTS["started"], "the scan finds part of it already")
    return s


def rank(gaps: dict, design, current_week: int) -> list[Score]:
    """Every unbuilt feature, scored. Buildable ones first by score, then the rest."""
    scores = [priority_score(g.feature, design, gaps, current_week)
              for g in gaps.values() if g.status != "present"]
    return sorted(scores, key=lambda s: (not s.eligible, -s.total, s.feature_id))
