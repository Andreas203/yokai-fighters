using Godot;

namespace YokaiFighters.Ui.Kit;

/// <summary>
/// Shared screen backdrop: flat indigo night gradient (<c>Night</c>), an optional art texture slot (<c>Art</c>, drawn cover-style over the
/// gradient) and a dim layer (<see cref="Dim"/> 0..1 of ink) for screens that sit over the fight.
/// </summary>
[Tool]
public partial class KitBackdrop : Control
{
	private float _dim;
	private Texture2D? _art;
	[Export(PropertyHint.Range, "0,1,0.05")] public float Dim { get => _dim; set { _dim = value; Apply(); } }
	[Export] public Texture2D? Art { get => _art; set { _art = value; Apply(); } }

	public override void _Ready() => Apply();

	private void Apply()
	{
		if (GetNodeOrNull<TextureRect>("Art") is { } art) { art.Texture = _art; art.Visible = _art != null; }
		if (GetNodeOrNull<ColorRect>("DimLayer") is { } d) { d.Color = new Color(0.114f, 0.106f, 0.129f, _dim); d.Visible = _dim > 0.001f; }
	}
}
