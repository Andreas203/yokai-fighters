using Godot;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-39: the bamboo grove at dusk (V9) is the scene <c>res://scenes/stage/bamboo_grove.tscn</c>, edited in Godot
/// (spec <c>assets/specs/stages/bamboo-grove-dusk.md</c>). Layers back to front: cropped backdrop quad, the lit lantern line
/// (<see cref="LanternLine"/>, YOK-208), ten bamboo clusters, two lit stone lanterns, the tiled ground strip; dusk = warm
/// low key light + cool fill + indigo depth fog. The bamboo and the two front lanterns are placed by hand (the layout
/// baked from the old seeded builder), the line is built from its parameters; this class holds the placement rules
/// StageTests checks against (every lantern of the line included), and the asset check for the placeholder fallback.
/// </summary>
public static class BambooGroveStage
{
	public const string Kind = "bamboo-grove";
	public const string DefaultDir = "res://assets/generated/stage/bamboo-grove/";

	/// <summary>Art inside the generated PNGs' grey letterbox border (asset-smith acceptance, in pixels); the scene crops by UV.</summary>
	public static readonly Rect2 BackdropArt = new(110, 75, 1156, 618), BackdropImage = new(0, 0, 1376, 768);
	public static readonly Rect2 GroundArt = new(103, 173, 1137, 422), GroundImage = new(0, 0, 1344, 768);

	/// <summary>No prop's front (centre z + footprint radius) comes nearer than this (F4). YOK-150: the shared <see cref="StageRules"/>.</summary>
	public const float PropMaxZ = StageRules.PropMaxZ;
	public const float PropOutlineMetres = StageRules.PropOutlineMetres;

	/// <summary>
	/// YOK-39 clear band (YOK-150: the shared <see cref="StageRules"/> values): behind the fighting area no prop's footprint
	/// comes nearer than <see cref="ClearBandBackZ"/>; lanterns stay at or behind <see cref="LanternMaxZ"/> everywhere.
	/// Lanterns sit by parallax so that in the standard framings each shows in a gap between the fighters: a lantern at
	/// depth z appears at about (x - camera x) x d / (d - z), d the camera's distance from the Z = 0 plane (about 6.7 m
	/// close, 9.8 m wide), so at the front lanterns' z -10.9 to -11.5 about x 0.38 close to x 0.47 wide.
	/// </summary>
	public const float ClearBandHalfX = StageRules.ClearBandHalfX, ClearBandBackZ = StageRules.ClearBandBackZ, LanternMaxZ = StageRules.LanternMaxZ;

	/// <summary>YOK-208: the stage's lantern light budget (<see cref="StageRules.MaxLanternLights"/>); <see cref="LanternLine"/> lights no more than what is left.</summary>
	public const int MaxLanternLights = StageRules.MaxLanternLights;

	/// <summary>The grove's backdrop and ground textures exist under <paramref name="dir"/> (else the fight uses the placeholder).</summary>
	public static bool AssetsPresent(string dir) =>
		ResourceLoader.Exists(dir + "backdrop.png") && ResourceLoader.Exists(dir + "ground-strip.png");

	/// <summary>Limit for a prop's front at <paramref name="x"/> (<see cref="StageRules.FrontLimit"/>).</summary>
	public static float FrontLimit(float x, bool lantern) => StageRules.FrontLimit(x, lantern);

	/// <summary>Bounds of every mesh under <paramref name="n"/> in its parent's space (<see cref="StageRules.LocalBounds"/>).</summary>
	public static Aabb LocalBounds(Node n) => StageRules.LocalBounds(n);
}
