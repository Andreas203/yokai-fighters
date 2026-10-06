using System;
using System.Collections.Generic;
using System.Text.Json;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-21: <c>properties.projectile</c> of a move (frame-data.schema.json). The move spawns one projectile
/// on <see cref="SpawnFrame"/>; it carries the move's damage, stun, pushback, strength and meter gain.
/// </summary>
/// <param name="Speed">Units per tick, toward the side the owner faced at spawn.</param>
/// <param name="Box">Hitbox relative to the spawn point (owner's feet at spawn), x toward the opponent.</param>
/// <param name="Hits">Hits it lands (or projectile hits it trades) before it vanishes.</param>
/// <param name="Lifetime">Ticks before it fades; 0 = until it leaves the screen.</param>
/// <param name="MaxActive">How many of the owner's projectiles may be on screen at once (default 1).</param>
public sealed record ProjectileData(int SpawnFrame, int Speed, Box Box, int Hits, int Lifetime = 0,
	bool PassesProjectiles = false, int AbsorbsProjectiles = 0, int MaxActive = 1);

/// <summary><c>properties.invuln[].against</c>.</summary>
[Flags]
public enum InvulnAgainst : byte
{
	None = 0,
	/// <summary>Strikes from an airborne attacker (Rising Talisman's anti-air frames).</summary>
	Air = 1,
	/// <summary>Every strike (body hitboxes).</summary>
	Strike = 2,
	Projectile = 4,
	/// <summary>Read but not applied: E12 keeps throws beating strike invulnerability; only burst avoids throws.</summary>
	Throw = 8,
	All = Air | Strike | Projectile,
}

public readonly record struct InvulnWindow(int First, int Last, InvulnAgainst Against);

/// <summary><c>cancel_windows[]</c>: frames where a held cancel rule (X4) may fire, and on which outcomes.</summary>
public readonly record struct CancelWindow(int First, int Last, bool OnHit, bool OnBlock, bool OnWhiff)
{
	public bool Covers(int frame) => frame >= First && frame <= Last;
}

public sealed partial record MoveData
{
	/// <summary>Non-null for a projectile move (Spirit Wave, Foxfire).</summary>
	public ProjectileData? Projectile { get; init; }
	public IReadOnlyList<InvulnWindow> Invuln { get; init; } = Array.Empty<InvulnWindow>();
	public IReadOnlyList<CancelWindow> CancelWindows { get; init; } = Array.Empty<CancelWindow>();

	/// <summary>True if a hit of this kind can't touch the move on that frame.</summary>
	public bool InvulnAt(int frame, InvulnAgainst kind)
	{
		foreach (var w in Invuln)
			if (frame >= w.First && frame <= w.Last && (w.Against & kind) != 0) return true;
		return false;
	}

	/// <summary>Reads projectile, invuln and cancel windows from a frame_data block (MoveLoader).</summary>
	internal static MoveData ReadSpecialProperties(MoveData move, JsonElement fd)
	{
		var invuln = new List<InvulnWindow>();
		ProjectileData? proj = null;
		if (fd.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
		{
			if (props.TryGetProperty("projectile", out var p) && p.ValueKind == JsonValueKind.Object)
			{
				int Req(string n) => p.TryGetProperty(n, out var v) ? v.GetInt32()
					: throw new FormatException($"{move.Id}: properties.projectile.{n} is required");
				int Opt(string n, int d) => p.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : d;
				var r = p.TryGetProperty("hitbox", out var hb) ? hb : throw new FormatException($"{move.Id}: properties.projectile.hitbox is required");
				proj = new ProjectileData(Req("spawn_frame"), Req("speed"),
					new Box(r.GetProperty("x").GetInt32(), r.GetProperty("y").GetInt32(), r.GetProperty("w").GetInt32(), r.GetProperty("h").GetInt32()),
					Req("hits"), Opt("lifetime", 0),
					p.TryGetProperty("passes_projectiles", out var pp) && pp.GetBoolean(),
					Opt("absorbs_projectiles", 0), Opt("max_active", 1));
				if (proj.SpawnFrame > move.TotalFrames)
					throw new FormatException($"{move.Id}: projectile spawn_frame {proj.SpawnFrame} is after the move ends");
				if (proj.Hits < 1 || proj.MaxActive < 1)
					throw new FormatException($"{move.Id}: projectile hits and max_active must be >= 1");
			}
			if (props.TryGetProperty("invuln", out var inv) && inv.ValueKind == JsonValueKind.Array)
				foreach (var w in inv.EnumerateArray())
				{
					var fr = w.GetProperty("frames");
					InvulnAgainst a = w.GetProperty("against").GetString() switch
					{
						"all" => InvulnAgainst.All,
						"air" => InvulnAgainst.Air,
						"strike" => InvulnAgainst.Strike,
						"projectile" => InvulnAgainst.Projectile,
						"throw" => InvulnAgainst.Throw,
						var o => throw new FormatException($"{move.Id}: invuln against '{o}' is unknown"),
					};
					invuln.Add(new InvulnWindow(fr[0].GetInt32(), fr[1].GetInt32(), a));
				}
		}
		var cancels = new List<CancelWindow>();
		if (fd.TryGetProperty("cancel_windows", out var cw) && cw.ValueKind == JsonValueKind.Array)
			foreach (var w in cw.EnumerateArray())
			{
				var fr = w.GetProperty("frames");
				bool hit = false, block = false, whiff = false;
				foreach (var on in w.GetProperty("on").EnumerateArray())
					switch (on.GetString())
					{
						case "hit": hit = true; break;
						case "block": block = true; break;
						case "whiff": whiff = true; break;
					}
				cancels.Add(new CancelWindow(fr[0].GetInt32(), fr[1].GetInt32(), hit, block, whiff));
			}
		return move with { Projectile = proj, Invuln = invuln.ToArray(), CancelWindows = cancels.ToArray() };
	}
}
