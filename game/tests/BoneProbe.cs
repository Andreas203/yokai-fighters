using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;
using YokaiFighters.Fight;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-33 measurement tool (not a test): poses each rigged fighter on every frame of every clip it plays and writes the
/// bone positions in gameplay units (200/m, feet-relative, x toward the opponent) as TSV, so hitboxes and hurtboxes can be
/// placed from the model. Run: godot --headless --path game res://tests/bone_probe.tscn -- out=&lt;file&gt;
/// </summary>
public partial class BoneProbe : Node
{
	public override void _Ready()
	{
		string path = "bone_probe.tsv";
		foreach (string a in OS.GetCmdlineUserArgs()) if (a.StartsWith("out=")) path = a[4..];
		var sb = new StringBuilder("fighter\tclip\tframe\tbone\tx\ty\tz\n");
		foreach (var set in new[] { FighterAnimSet.Ryo, FighterAnimSet.Kitsune })
		{
			var model = FighterModel.Create(set, FightScene.Catalog);
			if (model is null) continue;
			AddChild(model);
			var skel = model.Skeleton;
			var ids = new SortedSet<string>();
			foreach (var c in FightScene.Catalog.Clips.Keys) if (c.StartsWith(set.Fighter) || c.StartsWith("ryo")) ids.Add(c);
			foreach (string clip in ids)
			{
				var info = FightScene.Catalog[clip]!;
				if (!model.Has(clip)) continue;
				int n = info.FramesTotal > 0 ? info.FramesTotal : info.ComputedFrames;
				for (int f = 1; f <= n; f++)
				{
					model.Apply(new PoseSample(PoseKind.Attack, clip, f, null, 0, 1f), FightScene.Catalog, 1, 0);
					for (int b = 0; b < skel.GetBoneCount(); b++)
					{
						Vector3 p = (skel.GlobalTransform * skel.GetBoneGlobalPose(b)).Origin - model.GlobalPosition;
						sb.Append(set.Fighter).Append('\t').Append(clip).Append('\t').Append(f).Append('\t').Append(skel.GetBoneName(b)).Append('\t')
						  .Append((p.X * 200f).ToString("F1")).Append('\t').Append((p.Y * 200f).ToString("F1")).Append('\t').Append((p.Z * 200f).ToString("F1")).Append('\n');
					}
				}
			}
			model.QueueFree();
		}
		File.WriteAllText(path, sb.ToString());
		GD.Print("wrote " + path);
		GetTree().Quit();
	}
}
