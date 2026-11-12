namespace YokaiFighters.Fight;

/// <summary>
/// YOK-242 partial: projectile visuals and special-move effects (V8, A5), replacing the YOK-21 cyan box. Presentation
/// only: <see cref="Fx"/> listens to the match's events and observes it once per sim tick (from <c>StepSim</c>); the
/// layer draws from the sim state on every render. Nothing here is read by the sim, the AI, replays or the harness.
/// Hooks: <c>SetUpSpecialVfx()</c> in <c>_Ready</c>, <c>ObserveSpecialVfx()</c> in <c>StepSim</c>,
/// <c>RenderProjectiles()</c> in <c>Render</c>, <c>DetachSpecialVfx()</c> in <c>_ExitTree</c>. A replaced match is
/// picked up on its first render or step.
/// </summary>
public partial class FightScene
{
	/// <summary>The fight's projectile and special-move effects (the layer draws them every render).</summary>
	public SpecialFx Fx { get; } = new();

	public VfxLayer? FxLayer { get; private set; }

	private void SetUpSpecialVfx()
	{
		Fx.Presets = Presets;
		FxLayer = new VfxLayer { Name = VfxLayer.NodeName };
		AddChild(FxLayer);
	}

	private void FollowMatchVfx()
	{
		if (!ReferenceEquals(Fx.Attached, Match)) Fx.Attach(Match);
	}

	private void ObserveSpecialVfx()
	{
		FollowMatchVfx();
		Fx.Observe(Match);
	}

	/// <summary>Every projectile drawn over its data hitbox in its preset's look, then the live effects.</summary>
	private void RenderProjectiles()
	{
		FollowMatchVfx();
		FxLayer?.Draw(SpecialFx.Projectiles(Match, Fx.Presets), Fx.Visible(Match));
	}

	/// <summary>Stops listening to the match (from <c>_ExitTree</c>): a match that outlives the scene never calls back into it.</summary>
	private void DetachSpecialVfx() => Fx.Detach();
}
