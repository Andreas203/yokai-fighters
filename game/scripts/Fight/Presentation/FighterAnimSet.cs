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
/// The catalogued clips (data/clips) are cut on the Meshy 24-joint humanoid skeleton, so a Meshy body may borrow
/// another's clip (e.g. Kitsune's back dash uses Ryo's Step_Back) until her own is generated.
/// YOK-146: a body on another skeleton (the Tripo shared skeleton, Option A) names its own <see cref="ClipRoot"/>: it
/// plays the clip of the same file name from that folder, and never a Meshy file (the spine bones mean different things).
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
	/// <summary>Looping poses whose clip plays backwards (the Oni walks back on his forward walk reversed).</summary>
	public ISet<PoseKind> ReversedPoses { get; init; } = new HashSet<PoseKind>();
	/// <summary>Throw clip shown when a throw move's own clip is not catalogued.</summary>
	public string FallbackThrowClip { get; init; } = "ryo-generic-throw";
	public string? ClipFor(PoseKind kind) => Clips.TryGetValue(kind, out var c) ? c : null;

	public string MoveClip(string clip) => ClipOverrides.TryGetValue(clip, out var o) ? o : clip;

	/// <summary>
	/// YOK-146: res:// folder (ending in '/') holding this body's clips on its own skeleton, under the same file names as
	/// the catalog's Meshy GLBs; null = the body is on the Meshy skeleton and plays the catalog's files.
	/// </summary>
	public string? ClipRoot { get; init; }

	private readonly Dictionary<string, string?> _clipPaths = new();

	/// <summary>
	/// YOK-146: the GLB this body plays for a catalogued clip: the catalog's file on the Meshy skeleton, else the file of
	/// the same name under <see cref="ClipRoot"/>, or null when this skeleton has no such clip (the body never plays it).
	/// </summary>
	public string? ClipPath(ClipInfo info)
	{
		if (ClipRoot is null) return info.ResPath;
		if (_clipPaths.TryGetValue(info.ResPath, out var p)) return p;
		string own = ClipRoot + info.ResPath[(info.ResPath.LastIndexOf('/') + 1)..];
		p = Godot.ResourceLoader.Exists(own) ? own : null;
		_clipPaths[info.ResPath] = p;
		return p;
	}

	/// <summary>YOK-146: true when this body has the clip on its own skeleton (see <see cref="ClipPath"/>).</summary>
	public bool CanPlay(ClipInfo? info) => info != null && ClipPath(info) != null;

	private static readonly HashSet<string> SharedAir = new() { "ryo-jump", "ryo-jump-back", "kitsune-jump", "ryo-jump-kick", "ryo-jump-punch" };

	/// <summary>
	/// YOK-146 SWITCH: which Ryo body fights. True = the Tripo Ryo on the Option A shared skeleton (metre bones, his
	/// stopgap clips in <c>clips/ryo-tripo/</c>); false = the old Meshy Ryo (cm bones, <c>clips/ryo/</c>), for comparison.
	/// The Tanuki fights with Ryo's body (T0), so it follows. Read when a fight builds its models (the dev menu flips it).
	/// </summary>
	public static bool TripoRyo { get; set; } = true;

	/// <summary>Ryo's body for the next fight: <see cref="RyoTripo"/> or <see cref="RyoMeshy"/> by <see cref="TripoRyo"/>.</summary>
	public static FighterAnimSet Ryo => TripoRyo ? RyoTripo : RyoMeshy;

	/// <summary>YOK-146: the Tripo Ryo (assets/specs/rigs/ryo-tripo.md): same clip ids, trims and frame numbers as the Meshy Ryo.</summary>
	public static readonly FighterAnimSet RyoTripo = RyoBody(
		"res://assets/generated/characters/ryo/ryo-tripo-rigged.glb", "res://scenes/fighters/ryo-tripo.tscn",
		"res://assets/generated/clips/ryo-tripo/");

	/// <summary>The Meshy Ryo (YOK-53), kept for comparison until the Tripo set passes G4.</summary>
	public static readonly FighterAnimSet RyoMeshy = RyoBody(
		"res://assets/generated/characters/ryo/ryo-rigged.glb", "res://scenes/fighters/ryo.tscn", null);

	private static FighterAnimSet RyoBody(string model, string scene, string? clipRoot) => new()
	{
		Fighter = "ryo",
		ModelPath = model,
		ScenePath = scene,
		ClipRoot = clipRoot,
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
		FallbackThrowClip = "kitsune-throw",
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

	/// <summary>
	/// YOK-131: the Oni's own club kit (assets/specs/clips/oni-club-clips.md, data/clips/oni-*.json)
	/// plus the club-clean shared Ryo clips. Presentation only: the fight still runs the node's fighter (the Kitsune
	/// stand-in kit, node-scenes.json), so the Kitsune's move clips are mapped onto the Oni's takes by override.
	/// No Oni take yet for jump, landing, back dash or the burst cast: those stay on the Kitsune's / Ryo's clips.
	/// Quake (oni-quake) and Iron-Club Charge (oni-iron-club-charge) have clips but no move data to hook them to.
	/// </summary>
	public static readonly FighterAnimSet Oni = new()
	{
		Fighter = "oni",
		ModelPath = "res://assets/generated/characters/oni/oni-rigged.glb",
		ScenePath = "res://scenes/fighters/oni.tscn",
		JumpFrameOffset = Kitsune.JumpFrameOffset,
		LandingFrame = Kitsune.LandingFrame,
		BurstFrame = Kitsune.BurstFrame,
		AirClips = SharedAir,
		ReversedPoses = new HashSet<PoseKind> { PoseKind.WalkBack },
		ClipOverrides = new Dictionary<string, string>
		{
			["kitsune-light-punch"] = "ryo-light-punch",
			["kitsune-medium-punch"] = "oni-medium-punch",
			["kitsune-medium-kick"] = "oni-medium-kick",
			["kitsune-heavy-punch"] = "oni-heavy-punch",
			["kitsune-heavy-kick"] = "ryo-heavy-kick",
			["kitsune-throw"] = "ryo-generic-throw",
		},
		Clips = new Dictionary<PoseKind, string>
		{
			[PoseKind.Idle] = "oni-idle",
			[PoseKind.WalkFwd] = "oni-walk-fwd",
			[PoseKind.WalkBack] = "oni-walk-fwd", // played reversed (ReversedPoses)
			[PoseKind.Dash] = "oni-walk-fwd",
			[PoseKind.DashBack] = "ryo-dash-back", // borrowed: not in the Oni kit
			[PoseKind.Jump] = "kitsune-jump", // borrowed: the Oni kit has no jump
			[PoseKind.JumpBack] = "kitsune-jump",
			[PoseKind.Crouch] = "ryo-crouch",
			[PoseKind.BlockHigh] = "ryo-block-high",
			[PoseKind.BlockLow] = "oni-block-low",
			[PoseKind.HitHigh] = "oni-hit-high",
			[PoseKind.HitLow] = "oni-hit-low",
			[PoseKind.Knockdown] = "ryo-knockdown",
			[PoseKind.GetUp] = "ryo-get-up",
			[PoseKind.Thrown] = "oni-hit-high",
			[PoseKind.Landing] = "kitsune-jump",
			[PoseKind.Burst] = "kitsune-foxfire", // borrowed: no Oni cast take
			[PoseKind.Ko] = "ryo-knockdown",
			[PoseKind.Attack] = "ryo-light-punch",
		},
	};

	/// <summary>YOK-131: the Kappa's body on the Kitsune's shared clips (see <see cref="Oni"/>). He keeps them until he has his own kit. Known: feet sink 3-8 cm.</summary>
	public static readonly FighterAnimSet Kappa = Borrow(Kitsune, "kappa", "res://assets/generated/characters/kappa/kappa-rigged.glb", "res://scenes/fighters/kappa.tscn");

	private static FighterAnimSet Borrow(FighterAnimSet from, string fighter, string model, string scene) => new()
	{
		Fighter = fighter,
		ModelPath = model,
		ScenePath = scene,
		JumpFrameOffset = from.JumpFrameOffset,
		LandingFrame = from.LandingFrame,
		BurstFrame = from.BurstFrame,
		AirClips = from.AirClips,
		FallbackThrowClip = from.FallbackThrowClip,
		ClipOverrides = from.ClipOverrides,
		Clips = from.Clips,
	};

	public static FighterAnimSet For(string fighter) => fighter switch
	{
		"kitsune" => Kitsune,
		"oni" or "elder-oni" => Oni,
		"kappa" or "elder-kappa" => Kappa,
		_ => Ryo,
	};

	/// <summary>
	/// The body a fight shows for P2: the node's yokai when it has a body of its own (an Oni or Kappa node fought by the
	/// Kitsune stand-in kit shows the Oni or Kappa), else the fighter's. Presentation only; the sim never reads it.
	/// </summary>
	public static FighterAnimSet ForNode(string fighter, string? yokai) =>
		yokai != null && HasBody(yokai) ? For(yokai) : For(fighter);

	private static readonly HashSet<string> StandInBodies = new() { "oni", "elder-oni", "kappa", "elder-kappa" };

	/// <summary>True for a yokai whose node runs a stand-in kit but shows a body of its own (Oni, Kappa and their elders).</summary>
	public static bool HasBody(string yokai) => StandInBodies.Contains(yokai);
}
