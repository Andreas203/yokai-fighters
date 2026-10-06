using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-53: one measured clip (data/clips/&lt;id&gt;.json) as presentation needs it: which GLB holds the take, the
/// raw trim range (60 raw ticks per second) and the playback speed. Game frame n (1-based, F2) shows raw tick
/// <c>TrimStart + (n - 1) * Speed</c>, clamped to <c>TrimEnd</c>. Presentation only; the sim never reads it.
/// </summary>
public sealed record ClipInfo(string Id, string ResPath, int TrimStart, int TrimEnd, double Speed, int FramesTotal)
{
	/// <summary>Optional <c>yaw_offset</c> (degrees) from the clip file; presentation does not apply it yet.</summary>
	public double YawOffsetDeg { get; init; }

	public const double RawTicksPerSecond = 60.0;

	/// <summary>Raw tick shown on game frame <paramref name="frame"/> (clamped to 1..FramesTotal and the trim end).</summary>
	public double RawTickAt(int frame)
	{
		int n = Math.Clamp(frame, 1, FramesTotal);
		return Math.Min(TrimStart + (n - 1) * Speed, TrimEnd);
	}

	/// <summary>Seconds into the imported animation for game frame <paramref name="frame"/>.</summary>
	public double TimeAt(int frame) => RawTickAt(frame) / RawTicksPerSecond;

	/// <summary>Frames the trim and speed give (the clip file's frames_total should agree within one).</summary>
	public int ComputedFrames => (int)Math.Floor((TrimEnd - TrimStart) / Speed + 1e-9) + 1;
}

/// <summary>
/// YOK-53: reads every clip file. The structured fields <c>trim_start</c>/<c>trim_end</c>, <c>speed_scale</c>,
/// <c>file</c> (relative to game/) and <c>yaw_offset</c> win; each one missing falls back to the prose in <c>notes</c>:
/// the last "trim raw ticks A-B at Sx" sentence (a RE-TIMED pass supersedes the earlier trim) and the first
/// "file X.glb". Clips that resolve to neither are listed in <see cref="Problems"/>.
/// </summary>
public sealed class ClipCatalog
{
	private readonly Dictionary<string, ClipInfo> _clips = new();
	public List<string> Problems { get; } = new();
	public IReadOnlyDictionary<string, ClipInfo> Clips => _clips;
	public ClipInfo? this[string? id] => id != null && _clips.TryGetValue(id, out var c) ? c : null;
	public bool Has(string? id) => id != null && _clips.ContainsKey(id);

	private static readonly Regex TrimRx = new(
		@"[Tt]rim\b(?:\s*:)?(?:\s+raw)?(?:\s+60\s*fps)?(?:\s+ticks)?\s*:?\s*(?:ticks\s+)?(\d+)\s*-\s*(\d+)",
		RegexOptions.CultureInvariant);
	private static readonly Regex SpeedRx = new(@"(\d+(?:\.\d+)?)x\b", RegexOptions.CultureInvariant);
	private static readonly Regex SentenceEndRx = new(@"\.(?=\s|$)|;", RegexOptions.CultureInvariant);
	private static readonly Regex FileRx = new(@"\bfile\s+(\S+?\.glb)", RegexOptions.CultureInvariant);

	/// <summary>Parses the trim and speed from clip notes; null when no trim sentence is found.</summary>
	public static (int Start, int End, double Speed)? ParseTrim(string notes)
	{
		(int, int, double)? found = null;
		foreach (Match m in TrimRx.Matches(notes))
		{
			string rest = notes[(m.Index + m.Length)..];
			var end = SentenceEndRx.Match(rest);
			if (end.Success) rest = rest[..end.Index];
			var sp = SpeedRx.Match(rest);
			double speed = sp.Success ? double.Parse(sp.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 1.0;
			found = (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), speed);
		}
		return found;
	}

	/// <summary>The GLB named in the notes, as a path relative to <c>game/</c> (resolved against what exists).</summary>
	public static string? ResolveFile(string id, string notes, Func<string, bool> existsUnderGame)
	{
		var m = FileRx.Match(notes);
		if (!m.Success) return null;
		string file = m.Groups[1].Value.Replace('\\', '/');
		if (file.StartsWith("game/")) return existsUnderGame(file[5..]) ? file[5..] : null;
		string who = id.Split('-')[0];
		foreach (string dir in new[] { $"assets/generated/clips/{who}/", "assets/generated/clips/", $"assets/generated/characters/{who}/" })
			if (existsUnderGame(dir + file)) return dir + file;
		return null;
	}

	/// <summary>Loads data/clips/*.json; <paramref name="gameDir"/> is the Godot project folder (res://).</summary>
	/// <paramref name="notesOnly"/> ignores the structured fields (the pre-field behaviour; migration check).
	public static ClipCatalog Load(string clipsDir, string gameDir, bool notesOnly = false)
	{
		var cat = new ClipCatalog();
		if (!Directory.Exists(clipsDir)) { cat.Problems.Add($"no clips folder {clipsDir}"); return cat; }
		var files = Directory.GetFiles(clipsDir, "*.json");
		Array.Sort(files, StringComparer.Ordinal);
		foreach (string path in files)
		{
			using var doc = JsonDocument.Parse(File.ReadAllText(path));
			var root = doc.RootElement;
			string id = root.GetProperty("id").GetString()!;
			string notes = root.TryGetProperty("notes", out var n) ? n.GetString() ?? "" : "";
			int total = root.GetProperty("frames_total").GetInt32();
			bool Num(string key, out JsonElement v)
			{
				v = default;
				return !notesOnly && root.TryGetProperty(key, out v) && v.ValueKind == JsonValueKind.Number;
			}
			(int Start, int End, double Speed)? trim;
			if (Num("trim_start", out var ts) && Num("trim_end", out var te))
				trim = (ts.GetInt32(), te.GetInt32(), Num("speed_scale", out var sp) ? sp.GetDouble() : 1.0);
			else
			{
				trim = ParseTrim(notes);
				if (trim is { } t && Num("speed_scale", out var sp2)) trim = (t.Start, t.End, sp2.GetDouble());
			}
			Func<string, bool> exists = r => File.Exists(Path.Combine(gameDir, r));
			string? rel;
			if (!notesOnly && root.TryGetProperty("file", out var fe) && fe.ValueKind == JsonValueKind.String)
			{
				rel = fe.GetString()!.Replace('\\', '/');
				if (!exists(rel)) { cat.Problems.Add($"{id}: file game/{rel} not found"); continue; }
			}
			else rel = ResolveFile(id, notes, exists);
			if (trim is null) { cat.Problems.Add($"{id}: no trim (fields or notes)"); continue; }
			if (rel is null) { cat.Problems.Add($"{id}: GLB not found (fields or notes)"); continue; }
			var (s, e, speed) = trim.Value;
			double yaw = Num("yaw_offset", out var yw) ? yw.GetDouble() : 0.0;
			cat._clips[id] = new ClipInfo(id, "res://" + rel, s, e, speed, total) { YawOffsetDeg = yaw };
		}
		return cat;
	}

	/// <summary>Move id → clip id from data/moves/*.json (kind normal, throw or special).</summary>
	public static Dictionary<string, string> LoadMoveClips(string movesDir)
	{
		var map = new Dictionary<string, string>();
		if (!Directory.Exists(movesDir)) return map;
		foreach (string path in Directory.GetFiles(movesDir, "*.json"))
		{
			using var doc = JsonDocument.Parse(File.ReadAllText(path));
			var root = doc.RootElement;
			if (root.TryGetProperty("id", out var id) && root.TryGetProperty("clip", out var clip) && clip.ValueKind == JsonValueKind.String)
				map[id.GetString()!] = clip.GetString()!;
		}
		return map;
	}
}
