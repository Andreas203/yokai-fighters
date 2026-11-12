using System.Collections.Generic;

namespace YokaiFighters.Fight;

/// <summary>YOK-53: what the body is doing, read from sim state only (one per rendered pose segment).</summary>
public enum PoseKind
{
	Idle, WalkFwd, WalkBack, Dash, DashBack, Jump, JumpBack, Crouch, BlockHigh, BlockLow, HitHigh, HitLow,
	Knockdown, GetUp, Attack, Thrown, Landing, Burst, Ko,
}

/// <summary>YOK-finish-qa: one point of a <see cref="FighterAnimSet.LyingHipLift"/> curve: metres of hips lift at a game frame.</summary>
public readonly record struct HipLift(int Frame, float Meters);

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

	/// <summary>
	/// YOK-finish-qa Fix B: metres this body's hips are raised on every clip (added to the scaled <c>Hips</c> Y keys in
	/// <see cref="FighterModel"/>, after <see cref="FighterModel.HipScale"/>). Sized from the measured sink of the body on
	/// the shared clips (assets/specs/rigs/finish-qa.md): a wide or heavy body on clips cut for Ryo's proportions stands
	/// lower than its feet. Presentation only (F3): the sim never reads it.
	/// </summary>
	public float HipOffsetY { get; init; }

	/// <summary>Per clip id, metres used instead of <see cref="HipOffsetY"/>: a body's own take that already meets the floor
	/// (the Oni's heavy punch and Quake float 7 cm at his body offset).</summary>
	public IReadOnlyDictionary<string, float> ClipHipOffsetY { get; init; } = new Dictionary<string, float>();

	/// <summary>Metres the hips of one clip are raised: its <see cref="ClipHipOffsetY"/>, else <see cref="HipOffsetY"/>.</summary>
	public float HipOffsetFor(string clipId) => ClipHipOffsetY.TryGetValue(clipId, out float o) ? o : HipOffsetY;

	/// <summary>
	/// YOK-finish-qa Fix D (code): extra hips height over the game frames of these lying poses' clips (knockdown, get-up,
	/// KO), a curve of (game frame, metres) points, linear between them and held past the ends. Sized per frame from the
	/// measured sink of the lowest skinned point (FinishQaCapture), so a thick body (the Tanuki's belly and tail) on a thin
	/// body's lying clip keeps its lowest point on the floor. Poses that share a clip must share its curve.
	/// </summary>
	public IReadOnlyDictionary<PoseKind, HipLift[]> LyingHipLift { get; init; } = new Dictionary<PoseKind, HipLift[]>();

	/// <summary>The lying lift curve for a clip id (the first pose that shows it), or null.</summary>
	public HipLift[]? LyingLiftFor(string clipId)
	{
		foreach (var (kind, curve) in LyingHipLift)
			if (ClipFor(kind) == clipId && curve.Length > 0) return curve;
		return null;
	}

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
	/// The Tanuki true form (AM25) runs Ryo's Tripo clips whichever Ryo fights. Read when a fight builds its models (the dev menu flips it).
	/// </summary>
	public static bool TripoRyo { get; set; } = true;

	/// <summary>
	/// AM25 SWITCH: the Tanuki boss fight shows his true form (<see cref="TanukiTripo"/>) instead of Ryo's body. On by
	/// default: the designer kept the true form on 2026-11-11; he still runs Ryo's clips and moves (T0), and the sim still
	/// treats him as Ryo's body. Off shows Ryo's body, as before. Read when a fight builds its models (the dev menu flips it).
	/// </summary>
	public static bool TripoTanuki { get; set; } = true;

	/// <summary>The Tanuki's body for the next fight: his true form (<see cref="TripoTanuki"/>, AM25) or <see cref="Ryo"/>'s body.</summary>
	public static FighterAnimSet Tanuki => TripoTanuki ? TanukiTripo : Ryo;

	/// <summary>Ryo's body for the next fight: <see cref="RyoTripo"/> or <see cref="RyoMeshy"/> by <see cref="TripoRyo"/>.</summary>
	public static FighterAnimSet Ryo => TripoRyo ? RyoTripo : RyoMeshy;

	// Fix D curves (declared before the sets: static fields initialise in text order): the measured sink of the lowest skinned point per game frame plus 0.5 cm, simplified to within 2 cm above it.
	private static readonly HipLift[] TanukiKnockdownLift =
	{
		new(6, 0f), new(7, 0.03f), new(10, 0.08f), new(15, 0.20f), new(22, 0.52f), new(24, 0.57f), new(26, 0.57f),
		new(28, 0.55f), new(34, 0.53f), new(38, 0.56f), new(41, 0.56f),
	};
	private static readonly HipLift[] TanukiGetUpLift =
	{
		new(1, 0.11f), new(3, 0.16f), new(8, 0.32f), new(10, 0.38f), new(12, 0.43f), new(13, 0.43f), new(15, 0.40f),
		new(16, 0.35f), new(17, 0.28f), new(18, 0.20f), new(20, 0.14f), new(21, 0.09f), new(23, 0.02f), new(24, 0f),
	};
	private static readonly HipLift[] KappaGetUpLift =
	{
		new(1, 0.09f), new(4, 0.13f), new(6, 0.14f), new(11, 0.21f), new(13, 0.23f), new(15, 0.20f), new(16, 0.16f),
		new(17, 0.10f), new(19, 0.01f), new(20, 0f),
	};

	/// <summary>YOK-146: the Tripo Ryo (assets/specs/rigs/ryo-tripo.md): same clip ids, trims and frame numbers as the Meshy Ryo.</summary>
	public static readonly FighterAnimSet RyoTripo = RyoBody(
		"res://assets/generated/characters/ryo/ryo-tripo-rigged.glb", "res://scenes/fighters/ryo-tripo.tscn",
		"res://assets/generated/clips/ryo-tripo/");

	/// <summary>The Meshy Ryo (YOK-53), kept for comparison until the Tripo set passes G4.</summary>
	public static readonly FighterAnimSet RyoMeshy = RyoBody(
		"res://assets/generated/characters/ryo/ryo-rigged.glb", "res://scenes/fighters/ryo.tscn", null);

	/// <summary>
	/// AM25: the Tanuki's true form (assets/specs/rigs/tanuki.md v2b): 1.75 m, rigid tail on the hips, on the Tripo shared
	/// skeleton, playing Ryo's Tripo clips with Ryo's own overrides (he runs Ryo's moves and the copied special, T0). His
	/// hips follow his own rig (FighterModel.HipScale). Fighter id "tanuki"; the camera frames him like any other fighter.
	/// </summary>
	public static readonly FighterAnimSet TanukiTripo = RyoBody(
		"res://assets/generated/characters/tanuki/tanuki-v2-rigged.glb", "res://scenes/fighters/tanuki-v2.tscn",
		"res://assets/generated/clips/ryo-tripo/", "tanuki",
		// Fix D: his belly and rigid tail on Ryo's thin lying clips (hip scale 0.638) sank 56 cm (knockdown f25) and
		// 42 cm (get-up f13); finish-qa.md has the before and after.
		new Dictionary<PoseKind, HipLift[]> { [PoseKind.Knockdown] = TanukiKnockdownLift, [PoseKind.Ko] = TanukiKnockdownLift, [PoseKind.GetUp] = TanukiGetUpLift });


	private static FighterAnimSet RyoBody(string model, string scene, string? clipRoot, string fighter = "ryo",
		IReadOnlyDictionary<PoseKind, HipLift[]>? lyingLift = null) => new()
	{
		Fighter = fighter,
		LyingHipLift = lyingLift ?? new Dictionary<PoseKind, HipLift[]>(),
		ModelPath = model,
		ScenePath = scene,
		ClipRoot = clipRoot,
		JumpFrameOffset = 0,
		LandingFrame = 40,
		BurstFrame = 10,
		AirClips = SharedAir,
		// Every drafted (or Tanuki-copied, T0) special's clip on Ryo's body. YOK-226: the GREYBOX Ryo twins of the yokai
		// takes (data/clips ryo-*; the Oni's two are retimes of his heavy punch), each with the special's own frame count and
		// retime curve. SpecialClipTests covers every special in data.
		ClipOverrides = new Dictionary<string, string>
		{
			["kitsune-foxfire"] = "ryo-foxfire-cast",
			["kitsune-fox-mirage"] = "ryo-fox-mirage",
			["kappa-river-grab"] = "ryo-river-grab",
			["kappa-shell-spin"] = "ryo-shell-spin",
			["oni-iron-club-charge"] = "ryo-iron-club-charge",
			["oni-quake"] = "ryo-oni-quake",
		},
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
		// YOK-246: the Tripo takes are upright (the Meshy guard clips are crouched), so her feet floated 15 cm (light punch) and
		// 8 cm (Fox Mirage) in the fight pose (rig_qa); metres of hips lowered per clip, the median float of each take.
		ClipHipOffsetY = new Dictionary<string, float> { ["kitsune-light-punch"] = -0.147f, ["kitsune-fox-mirage"] = -0.05f },
		Clips = new Dictionary<PoseKind, string>
		{
			[PoseKind.Idle] = "kitsune-idle",
			[PoseKind.WalkFwd] = "kitsune-walk-guard-fwd",
			[PoseKind.WalkBack] = "kitsune-walk-guard-back",
			[PoseKind.Dash] = "kitsune-walk-guard-fwd",
			[PoseKind.DashBack] = "kitsune-dash-back", // YOK-246: her own retarget of the Meshy 14 Step_Back (was Ryo's clip)
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
		HipOffsetY = 0.08f, // Fix B: his feet sank 7-16 cm on every clip (idle 9.5); finish-qa.md
		// His own slam takes meet the floor at 0 (sink 7 cm on f1, float 4 cm): split between the two.
		ClipHipOffsetY = new Dictionary<string, float> { ["oni-heavy-punch"] = 0.02f, ["oni-quake"] = 0.02f, ["oni-iron-club-charge"] = 0.05f },
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
			[PoseKind.DashBack] = "oni-dash-back", // YOK-246: retargeted onto the Oni, club-checked
			[PoseKind.Jump] = "kitsune-jump", // borrowed: the Oni kit has no jump
			[PoseKind.JumpBack] = "kitsune-jump",
			[PoseKind.Crouch] = "ryo-crouch", // YOK-247: oni-crouch put the club through his thigh (8-13 cm); kept the old clip
			[PoseKind.BlockHigh] = "ryo-block-high", // YOK-247: oni-block-high put the club through his head (13.6 cm); kept the old clip
			[PoseKind.BlockLow] = "oni-block-low",
			[PoseKind.HitHigh] = "oni-hit-high",
			[PoseKind.HitLow] = "oni-hit-low",
			[PoseKind.Knockdown] = "ryo-knockdown",
			[PoseKind.GetUp] = "oni-get-up",
			[PoseKind.Thrown] = "oni-hit-high",
			[PoseKind.Landing] = "kitsune-jump",
			[PoseKind.Burst] = "kitsune-foxfire", // borrowed: no Oni cast take
			[PoseKind.Ko] = "ryo-knockdown",
			[PoseKind.Attack] = "ryo-light-punch",
		},
	};

	/// <summary>
	/// YOK-131: the Kappa's body on the Kitsune's shared pose clips (idle, walk, block, hit, jump; see <see cref="Oni"/>).
	/// YOK-228: his own moves (data/moves/kappa-*.json) name their own clip files (data/clips/kappa-*.json, GREYBOX reuse of
	/// Kitsune and Ryo takes, route A on YOK-219), which the Kitsune's overrides never touch. Known: feet sink 3-8 cm.
	/// </summary>
	public static readonly FighterAnimSet Kappa = Borrow(Kitsune, "kappa", "res://assets/generated/characters/kappa/kappa-rigged.glb", "res://scenes/fighters/kappa.tscn",
		new Dictionary<PoseKind, string>
		{
			// YOK-246: the Kappa's own pose clips (data/clips/kappa-*.json, retargeted onto his skeleton and measured), no longer the Kitsune's.
			[PoseKind.Idle] = "kappa-idle",
			[PoseKind.WalkFwd] = "kappa-walk-guard-fwd",
			[PoseKind.WalkBack] = "kappa-walk-guard-back",
			[PoseKind.Dash] = "kappa-walk-guard-fwd",
			[PoseKind.DashBack] = "kappa-dash-back",
			[PoseKind.Crouch] = "kappa-crouch",
			[PoseKind.BlockHigh] = "kappa-block-high",
			[PoseKind.BlockLow] = "kappa-block-low",
			[PoseKind.GetUp] = "kappa-get-up",
		},
		hipOffset: 0.03f, // Fix B: feet sank about 3 cm (idle 2.8 median); finish-qa.md
		// YOK-246: his three accepted Tripo takes are upright (floated 14-20 cm at the Fix B offset): the median float per clip.
		clipHipOffsetY: new Dictionary<string, float> { ["kappa-light-punch"] = -0.131f, ["kappa-heavy-punch"] = -0.163f, ["kappa-light-kick"] = -0.108f },
		lyingLift: new Dictionary<PoseKind, HipLift[]> { [PoseKind.GetUp] = KappaGetUpLift }); // Fix D: legs 25 cm under the floor at get-up f13

	private static IReadOnlyDictionary<PoseKind, string> Merge(IReadOnlyDictionary<PoseKind, string> from, Dictionary<PoseKind, string>? own)
	{
		if (own is null) return from;
		var all = new Dictionary<PoseKind, string>();
		foreach (var kv in from) all[kv.Key] = kv.Value;
		foreach (var kv in own) all[kv.Key] = kv.Value;
		return all;
	}

	private static FighterAnimSet Borrow(FighterAnimSet from, string fighter, string model, string scene, Dictionary<PoseKind, string>? own = null,
		float hipOffset = 0f, IReadOnlyDictionary<PoseKind, HipLift[]>? lyingLift = null,
		IReadOnlyDictionary<string, float>? clipHipOffsetY = null) => new()
	{
		Fighter = fighter,
		HipOffsetY = hipOffset,
		ClipHipOffsetY = clipHipOffsetY ?? new Dictionary<string, float>(),
		LyingHipLift = lyingLift ?? new Dictionary<PoseKind, HipLift[]>(),
		ModelPath = model,
		ScenePath = scene,
		JumpFrameOffset = from.JumpFrameOffset,
		LandingFrame = from.LandingFrame,
		BurstFrame = from.BurstFrame,
		AirClips = from.AirClips,
		FallbackThrowClip = from.FallbackThrowClip,
		ClipOverrides = from.ClipOverrides,
		Clips = Merge(from.Clips, own),
	};

	public static FighterAnimSet For(string fighter) => fighter switch
	{
		"kitsune" => Kitsune,
		"tanuki" => Tanuki, // his true form (AM25) unless TripoTanuki is off
		"oni" or "elder-oni" => Oni,
		"kappa" or "elder-kappa" => Kappa,
		_ => Ryo,
	};

	/// <summary>
	/// The body a fight shows for P2: the node's yokai when it has a body of its own (an Oni or Kappa node, whose fighter in
	/// node-scenes.json is the Kitsune fallback, shows the Oni or Kappa), else the fighter's. Presentation only; the sim never reads it.
	/// </summary>
	public static FighterAnimSet ForNode(string fighter, string? yokai) =>
		yokai != null && HasBody(yokai) ? For(yokai) : For(fighter);

	private static readonly HashSet<string> StandInBodies = new() { "oni", "elder-oni", "kappa", "elder-kappa" };

	/// <summary>True for a yokai whose node names a stand-in fighter but shows a body of its own (Oni, Kappa and their elders).</summary>
	public static bool HasBody(string yokai) => StandInBodies.Contains(yokai);
}
