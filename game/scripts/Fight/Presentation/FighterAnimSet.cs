using System.Collections.Generic;

namespace YokaiFighters.Fight;

/// <summary>YOK-53: what the body is doing, read from sim state only (one per rendered pose segment).</summary>
public enum PoseKind
{
	Idle, WalkFwd, WalkBack, Dash, DashBack, Jump, JumpBack, Crouch, BlockHigh, BlockLow, HitHigh, HitLow,
	Knockdown, GetUp, Attack, Thrown, Landing, Burst, Ko,
}

/// <summary>
/// YOK-53: a fighter's model and which clip shows each <see cref="PoseKind"/> (presentation data, not game rules).
/// Every clip shares the Meshy 24-joint humanoid skeleton, so a fighter may borrow another's clip (e.g. Kitsune's
/// back dash uses Ryo's Step_Back) until her own is generated.
/// </summary>
public sealed class FighterAnimSet
{
	public required string Fighter { get; init; }
	/// <summary>Rigged model (res://).</summary>
	public required string ModelPath { get; init; }
	/// <summary>
	/// YOK-39: the fighter's body scene (rigged model, mirror/yaw, Animator, toon look, tails), edited in Godot and
	/// instanced by <see cref="FighterModel.Create"/>. The guard-stance yaw and the Kitsune's tails live there now.
	/// </summary>
	public required string ScenePath { get; init; }
	public required IReadOnlyDictionary<PoseKind, string> Clips { get; init; }
	/// <summary>Move clip overrides for this body (e.g. Ryo casting Foxfire uses his own cast take).</summary>
	public IReadOnlyDictionary<string, string> ClipOverrides { get; init; } = new Dictionary<string, string>();
	/// <summary>Jump clip frame on the sim's first airborne frame minus one (Kitsune's take leaves the floor on frame 3).</summary>
	public int JumpFrameOffset { get; init; }
	/// <summary>First landing frame in the jump clip, shown by the Landing state (E11) and stepped from there.</summary>
	public int LandingFrame { get; init; }
	/// <summary>First burst frame inside the burst clip (the cast's release pose).</summary>
	public int BurstFrame { get; init; } = 1;
	/// <summary>Clips that play airborne: their hips are not allowed above the rest height (the sim draws the arc).</summary>
	public ISet<string> AirClips { get; init; } = new HashSet<string>();
	public string? ClipFor(PoseKind kind) => Clips.TryGetValue(kind, out var c) ? c : null;

	public string MoveClip(string clip) => ClipOverrides.TryGetValue(clip, out var o) ? o : clip;

	private static readonly HashSet<string> SharedAir = new() { "ryo-jump", "ryo-jump-back", "kitsune-jump", "ryo-jump-kick", "ryo-jump-punch" };

	public static readonly FighterAnimSet Ryo = new()
	{
		Fighter = "ryo",
		ModelPath = "res://assets/generated/characters/ryo/ryo-rigged.glb",
		ScenePath = "res://scenes/fighters/ryo.tscn",
		JumpFrameOffset = 0,
		LandingFrame = 40,
		BurstFrame = 10,
		AirClips = SharedAir,
		ClipOverrides = new Dictionary<string, string> { ["kitsune-foxfire"] = "ryo-foxfire-cast" },
		Clips = new Dictionary<PoseKind, string>
		{
			[PoseKind.Idle] = "ryo-idle-guard",
			[PoseKind.WalkFwd] = "ryo-walk-guard-fwd",
			[PoseKind.WalkBack] = "ryo-walk-guard-back",
			[PoseKind.Dash] = "ryo-walk-guard-fwd",
			[PoseKind.DashBack] = "ryo-dash-back",
			[PoseKind.Jump] = "ryo-jump",
			[PoseKind.JumpBack] = "ryo-jump-back",
			[PoseKind.Crouch] = "ryo-crouch",
			[PoseKind.BlockHigh] = "ryo-block-high",
			[PoseKind.BlockLow] = "ryo-block-low",
			[PoseKind.HitHigh] = "ryo-hit-high",
			[PoseKind.HitLow] = "ryo-hit-low",
			[PoseKind.Knockdown] = "ryo-knockdown",
			[PoseKind.GetUp] = "ryo-get-up",
			[PoseKind.Thrown] = "ryo-hit-high",
			[PoseKind.Landing] = "ryo-jump",
			[PoseKind.Burst] = "ryo-spirit-wave-cast",
			[PoseKind.Ko] = "ryo-knockdown",
			[PoseKind.Attack] = "ryo-light-punch", // fallback for a move with no clip (TEST FIXTURE moves)
		},
	};

	public static readonly FighterAnimSet Kitsune = new()
	{
		Fighter = "kitsune",
		ModelPath = "res://assets/generated/characters/kitsune/kitsune-rigged.glb",
		ScenePath = "res://scenes/fighters/kitsune.tscn",
		JumpFrameOffset = 2,
		LandingFrame = 37,
		BurstFrame = 10,
		AirClips = SharedAir,
		Clips = new Dictionary<PoseKind, string>
		{
			[PoseKind.Idle] = "kitsune-idle",
			[PoseKind.WalkFwd] = "kitsune-walk-guard-fwd",
			[PoseKind.WalkBack] = "kitsune-walk-guard-back",
			[PoseKind.Dash] = "kitsune-walk-guard-fwd",
			[PoseKind.DashBack] = "ryo-dash-back", // borrowed: no Kitsune back-dash take yet
			[PoseKind.Jump] = "kitsune-jump",
			[PoseKind.JumpBack] = "kitsune-jump",
			[PoseKind.Crouch] = "kitsune-crouch",
			[PoseKind.BlockHigh] = "kitsune-block-high",
			[PoseKind.BlockLow] = "kitsune-block-low",
			[PoseKind.HitHigh] = "kitsune-hit-high",
			[PoseKind.HitLow] = "kitsune-hit-low",
			[PoseKind.Knockdown] = "kitsune-knockdown",
			[PoseKind.GetUp] = "kitsune-get-up",
			[PoseKind.Thrown] = "kitsune-hit-high",
			[PoseKind.Landing] = "kitsune-jump",
			[PoseKind.Burst] = "kitsune-foxfire",
			[PoseKind.Ko] = "kitsune-knockdown",
			[PoseKind.Attack] = "kitsune-light-punch",
		},
	};

	public static FighterAnimSet For(string fighter) => fighter == "kitsune" ? Kitsune : Ryo;
}
