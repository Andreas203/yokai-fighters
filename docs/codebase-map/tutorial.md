# Tutorial: guided apprentice fight

`game/scripts/Tutorial/`, namespace `YokaiFighters.Tutorial`. Y6. Pure C#: no Godot, clock, floats or randomness (`TutorialTests` scans the folder). Scene side is `Fight/FightScene.Tutorial.cs` ([fight.md](fight.md)). Index: [README.md](README.md).

| File | What it is and its public surface |
|---|---|
| `Tutorial/ApprenticeTutorial.cs` | Lessons over a `Match`: `Order` = Block, Throw, ThrowBreak, Meter, Burst, Cancel. `Observe()` after every `Match.Step`; only what happens while a lesson is on screen counts; at least `MinPromptTicks`. `SkipLesson()`/`SkipAll()`, `Unavailable` (burst spent, C6; no cancel rule, X1), `Detach()` (takes back a lent rule only at KO/reset/new match, F3). Events `LessonEnded`, `Completed`. `TutorialOptions`: every number **provisional**; `LentCancel` (default null) is the **open** X1-vs-Y6 question. |
| `Tutorial/TutorialCoach.cs` | The apprentice's half: `Next(AiView, lesson)` through a `ReactionFrames` delay ring of screen + lesson (P6, Y4 30f). Block/Burst = walk in and swing; ThrowBreak = throw every `ThrowRetryTicks`; others stand still; null = the profile AI plays. No `Match`/`Fighter`/input reference. |
| `Tutorial/TutorialPrompts.cs` | **Placeholder** prompt per lesson + skip hint, prefixed `[PLACEHOLDER] ` (habit-writer owns the copy). |

Wiring: `GameFlow.BeginFight` sets `TutorialAllowed = !Saved.Story.Has(StoryProgress.ApprenticeTutorial)`; `MarkTutorialSeen()` on finish, node completion or run end. Replays carry `ReplayHeader.Tutorial` and `tutorial-skip` events.
