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

	/// <summary>YOK-113: optional retiming curve (F2, AM5); when set it replaces <see cref="Speed"/>. Contiguous,
	/// frame 1..FramesTotal, checked by <see cref="ClipCatalog.CheckRetime"/> before a clip gets one.</summary>
	public IReadOnlyList<RetimeSegment>? Retime { get; init; }

	public const double RawTicksPerSecond = 60.0;

	/// <summary>Raw tick shown on game frame <paramref name="frame"/> (clamped to 1..FramesTotal and the trim end).
	/// With a curve: the segment holding the frame, <c>ta + (tb-ta)(f-a)/(b-a)</c> from an exact integer numerator,
	/// so the same frame always gives the same tick (F3).</summary>
	public double RawTickAt(int frame)
	{
		int n = Math.Clamp(frame, 1, FramesTotal);
		if (Retime is { Count: > 0 } curve)
		{
			foreach (var seg in curve)
				if (n <= seg.LastFrame) return Math.Min(seg.TickAt(n), TrimEnd);
			return Math.Min(curve[^1].LastTick, TrimEnd);
		}
		return Math.Min(TrimStart + (n - 1) * Speed, TrimEnd);
	}

	/// <summary>Seconds into the imported animation for game frame <paramref name="frame"/>.</summary>
	public double TimeAt(int frame) => RawTickAt(frame) / RawTicksPerSecond;

	/// <summary>Frames the trim and speed give (the clip file's frames_total should agree within one).</summary>
	public int ComputedFrames => Retime is { Count: > 0 } curve
		? curve[^1].LastFrame
		: (int)Math.Floor((TrimEnd - TrimStart) / Speed + 1e-9) + 1;
}

/// <summary>YOK-113: one retiming segment: game frames <c>FirstFrame..LastFrame</c> (inclusive) show raw ticks
/// <c>FirstTick..LastTick</c> linearly; neighbours share their boundary frame and tick.</summary>
public readonly record struct RetimeSegment(int FirstFrame, int LastFrame, int FirstTick, int LastTick)
{
	public double TickAt(int frame)
	{
		int f = Math.Clamp(frame, FirstFrame, LastFrame);
		return FirstTick + (double)(((long)LastTick - FirstTick) * ((long)f - FirstFrame)) / ((long)LastFrame - FirstFrame);
	}
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
	public static ClipCatalog Load(string clipsDir, string gameDir, bool notesOnly = false) =>
		Load(clipsDir, r => File.Exists(Path.Combine(gameDir, r)), notesOnly);

	/// <summary>YOK-39: <paramref name="existsUnderGame"/> checks a path relative to res:// (the exported build packs GLBs in the PCK).</summary>
	public static ClipCatalog Load(string clipsDir, Func<string, bool> existsUnderGame, bool notesOnly = false)
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
			// YOK-113: a retime curve replaces speed_scale; without trim fields its first/last tick is the trim.
			List<RetimeSegment>? retime = null;
			if (!notesOnly && root.TryGetProperty("retime", out var rt))
			{
				(int, int)? trimFields = Num("trim_start", out var rts) && Num("trim_end", out var rte)
					&& rts.TryGetInt32(out int rs) && rte.TryGetInt32(out int re) ? (rs, re) : null;
				string? why = ParseRetime(rt, out retime) ?? CheckRetime(retime!, total, trimFields);
				if (why != null) { cat.Problems.Add($"{id}: retime {why}"); continue; }
			}
			(int Start, int End, double Speed)? trim;
			if (Num("trim_start", out var ts) && Num("trim_end", out var te))
				trim = (ts.GetInt32(), te.GetInt32(), Num("speed_scale", out var sp) ? sp.GetDouble() : 1.0);
			else if (retime != null)
				trim = (retime[0].FirstTick, retime[^1].LastTick, 1.0);
			else
			{
				trim = ParseTrim(notes);
				if (trim is { } t && Num("speed_scale", out var sp2)) trim = (t.Start, t.End, sp2.GetDouble());
			}
			Func<string, bool> exists = existsUnderGame;
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
			cat._clips[id] = new ClipInfo(id, "res://" + rel, s, e, speed, total) { YawOffsetDeg = yaw, Retime = retime };
		}
		return cat;
	}

	/// <summary>YOK-113 first-pass limits (F2): a segment plays 0.5x-2x or holds; a held tick shows on at most
	/// 4 game frames. Kept in step with tools/validate_data.py (RETIME_*).</summary>
	public const int RetimeMaxHoldFrames = 4;

	/// <summary>Reads a <c>retime</c> JSON array; returns why it is malformed, or null.</summary>
	public static string? ParseRetime(JsonElement arr, out List<RetimeSegment>? segs)
	{
		segs = null;
		if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0) return "must be a non-empty list of segments";
		var list = new List<RetimeSegment>();
		int i = 0;
		foreach (var el in arr.EnumerateArray())
		{
			if (el.ValueKind != JsonValueKind.Object
				|| !Pair(el, "frames", out int a, out int b) || !Pair(el, "ticks", out int ta, out int tb))
				return $"segment {i} needs frames [a, b] and ticks [ta, tb] as integers";
			// YOK-164: the schema's segment allows only frames, ticks and a string note; refuse the rest like it does.
			foreach (var prop in el.EnumerateObject())
				if (prop.Name is not ("frames" or "ticks") && (prop.Name != "note" || prop.Value.ValueKind != JsonValueKind.String))
					return $"segment {i}: unexpected {(prop.Name == "note" ? "non-string note" : $"key '{prop.Name}'")}";
			list.Add(new RetimeSegment(a, b, ta, tb));
			i++;
		}
		segs = list;
		return null;

		static bool Pair(JsonElement o, string key, out int x, out int y)
		{
			x = y = 0;
			if (!o.TryGetProperty(key, out var p) || p.ValueKind != JsonValueKind.Array || p.GetArrayLength() != 2) return false;
			return p[0].ValueKind == JsonValueKind.Number && p[0].TryGetInt32(out x)
				&& p[1].ValueKind == JsonValueKind.Number && p[1].TryGetInt32(out y);
		}
	}

	/// <summary>Contiguous from frame 1 to <paramref name="framesTotal"/>, each segment 0.5x-2x (exact integer
	/// compare) or a hold, a held tick on at most <see cref="RetimeMaxHoldFrames"/> frames, and every tick inside
	/// <paramref name="trim"/> (the trim_start/trim_end fields) when set; returns why not, or null.</summary>
	public static string? CheckRetime(IReadOnlyList<RetimeSegment> segs, int framesTotal, (int Start, int End)? trim = null)
	{
		if (segs.Count == 0) return "must be a non-empty list of segments";
		if (segs[0].FirstFrame != 1) return $"must start on game frame 1, starts on {segs[0].FirstFrame}";
		if (segs[^1].LastFrame != framesTotal) return $"must end on frames_total {framesTotal}, ends on {segs[^1].LastFrame}";
		int holdFrom = -1;
		for (int i = 0; i < segs.Count; i++)
		{
			var s = segs[i];
			if (s.FirstTick < 0 || s.LastTick < 0) return $"segment {i}: negative tick";
			if (trim is { } t && (Math.Min(s.FirstTick, s.LastTick) < t.Start || Math.Max(s.FirstTick, s.LastTick) > t.End))
				return $"segment {i}: ticks {s.FirstTick}-{s.LastTick} fall outside trim {t.Start}-{t.End}";
			long df = (long)s.LastFrame - s.FirstFrame, dt = (long)s.LastTick - s.FirstTick; // long: no int32 overflow on huge segments
			if (df <= 0) return $"segment {i}: last frame must be after first ({s.FirstFrame}-{s.LastFrame})";
			if (i > 0 && (s.FirstFrame != segs[i - 1].LastFrame || s.FirstTick != segs[i - 1].LastTick))
				return $"segment {i}: not contiguous with segment {i - 1}";
			if (dt == 0)
			{
				if (holdFrom < 0) holdFrom = s.FirstFrame;
				if (((long)s.LastFrame - holdFrom + 1) > RetimeMaxHoldFrames)
					return $"segment {i}: held tick {s.FirstTick} shows on {((long)s.LastFrame - holdFrom + 1)} frames (max {RetimeMaxHoldFrames})";
			}
			else
			{
				holdFrom = -1;
				if (2L * dt < df || dt > 2L * df) return $"segment {i}: plays at {(double)dt / df:0.###}x, outside 0.5x-2x";
			}
		}
		return null;
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
