using System.Collections.Generic;
using System.Linq;
using YokaiFighters.Ai;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-226 (code-critic advisories): a rare special held back off an elder node (NodeFight.HeldBack) in a shared
/// temperament profile. As a behaviour it passes the profile check and is skipped before the weighted draw; as the habit
/// it is still reported missing (Y2: a fired habit always comes out). Profiles here are TEST FIXTURES.
/// </summary>
public static class HeldBackSpecialTests
{
	static string Json(string habitDo, string behaviours) => $$"""
		{ "kind": "profile", "id": "test-held-back", "yokai": "kitsune", "temperament": "patient",
		  "reaction": { "tier": "apprentice", "frames": 30 }, "aggression_bias": 50, "preferred_range": "close",
		  "habit": { "id": "test-habit", "description": "d", "when": "self_got_up", "do": "{{habitDo}}", "chance_pct": 60 },
		  "behaviours": [ {{behaviours}} ], "rules": ["Y4"] }
		""";

	static MoveData Jab => MoveLoader.Parse("""
		{ "kind": "normal", "id": "test-held-jab", "input": { "button": "LP" }, "frame_data": {
		  "startup": 3, "active": 2, "recovery": 6, "damage": 20, "hitstun": 12, "blockstun": 9,
		  "hitboxes": [ { "frames": [4, 5], "rect": { "x": 0, "y": 100, "w": 120, "h": 60 } } ] } }
		""");

	const string JabAtClose = """{ "when": "at_close_range", "do": "normal:test-held-jab", "weight": 20 }""";
	const string RareAtClose = """{ "when": "at_close_range", "do": "special:test-rare", "weight": 100 }""";

	[Test]
	public static void Missing_ExemptsAHeldBackRareInABehaviour_NeverAsTheHabit()
	{
		var m = new Match(null, new[] { Jab }, new[] { Jab });
		var held = new[] { "test-rare" };
		var asBehaviour = BehaviourProfile.Parse(Json("jump", JabAtClose + "," + RareAtClose));
		Assert.Equal(0, AiLoadout.From(m.P2, held).Missing(asBehaviour).Count, "a held-back rare in a behaviour passes");
		Assert.Equal(1, AiLoadout.From(m.P2).Missing(asBehaviour).Count, "not held back: reported");
		var asHabit = BehaviourProfile.Parse(Json("special:test-rare", JabAtClose));
		Assert.Equal("special:test-rare", string.Join(",", AiLoadout.From(m.P2, held).Missing(asHabit)), "as the habit: still missing (Y2)");
	}

	/// <summary>The same AI with and without a behaviour it can't do plays the same inputs: its weight never reaches the draw.</summary>
	[Test]
	public static void Neutral_SkipsAnUndoableBehaviourBeforeTheDraw()
	{
		List<FighterInput> Play(string behaviours)
		{
			var m = new Match(null, new[] { Jab }, new[] { Jab });
			m.P1.X = -60 * SimConfig.Scale;
			m.P2.X = 60 * SimConfig.Scale;
			var ai = new ProfileAi(BehaviourProfile.Parse(Json("jump", behaviours)), 1, AiLoadout.From(m.P2, new[] { "test-rare" }), 7);
			var outs = new List<FighterInput>();
			for (int t = 0; t < 600; t++) { var o = ai.Next(AiView.Capture(m)); outs.Add(o); m.Step(FighterInput.None, o); }
			return outs;
		}
		var plain = Play(JabAtClose);
		var withRare = Play(JabAtClose + "," + RareAtClose);
		Assert.True(plain.Any(o => o.Move != 0 || o.Has(InputBits.LightPunch)), "the jab is picked at all");
		Assert.True(plain.SequenceEqual(withRare), "an undoable behaviour changes nothing: skipped before the draw");
	}
}
