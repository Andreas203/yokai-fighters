using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using YokaiFighters.Sim;

namespace YokaiFighters.Replay;

/// <summary>A move in a fighter's slot list: its id and a content fingerprint, so a replay fails clearly when data changed.</summary>
public sealed class KitEntry
{
	public string Id { get; set; } = "";
	public string Fp { get; set; } = "";
}

/// <summary>An equipped special (run state at fight start): slot A-D, level, modifier, and the built moves' fingerprints.</summary>
public sealed class SpecialEntry
{
	public int Slot { get; set; }
	public string Id { get; set; } = "";
	public int Level { get; set; }
	public string? Modifier { get; set; }
	public string MoveFp { get; set; } = "";
	public string? ExFp { get; set; }
}

public sealed class CancelEntry
{
	public string Id { get; set; } = "";
	public CancelFrom From { get; set; }
	public CancelInto Into { get; set; }
	public bool OnHit { get; set; }
	public bool OnBlock { get; set; }
	public bool OnWhiff { get; set; }
}

public sealed class FighterSetup
{
	public string Scheme { get; set; } = "";
	public List<KitEntry> Moves { get; set; } = new();
	public List<SpecialEntry> Specials { get; set; } = new();
	public List<CancelEntry> CancelRules { get; set; } = new();
}

/// <summary>P2's AI as built: profile (with fingerprint), reaction frames (Y4) and seed. Null = devices/recorded input.</summary>
public sealed class AiSetup
{
	public string ProfileId { get; set; } = "";
	public string Temperament { get; set; } = "";
	public string ProfileFp { get; set; } = "";
	public int ReactionFrames { get; set; }
	public uint Seed { get; set; }
}

/// <summary>A mid-fight change applied before step <see cref="Tick"/> (0-based step index): scheme flip (F4) or AI rebuild/toggle (F6/F7).</summary>
public sealed class ReplayEvent
{
	public int Tick { get; set; }
	public string Kind { get; set; } = ""; // "scheme" | "ai"
	public int Player { get; set; }
	public string? Scheme { get; set; }
	public AiSetup? Ai { get; set; }
}

public sealed class ReplayResult
{
	public int Steps { get; set; }
	public int MatchTick { get; set; }
	public string Phase { get; set; } = "";
	public int Winner { get; set; }
	public int P1Health { get; set; }
	public int P2Health { get; set; }
	public string FinalHash { get; set; } = "";
}

/// <summary>Everything needed to rebuild the fight at tick 0. Ids + fingerprints, never the data itself.</summary>
public sealed class ReplayHeader
{
	public int Version { get; set; } = ReplayRecording.FormatVersion;
	public string ConfigFp { get; set; } = "";
	public int P1MaxHealth { get; set; }
	public int P2MaxHealth { get; set; }
	public int? P1StartHealth { get; set; }
	public int? P2StartHealth { get; set; }
	public FighterSetup[] Fighters { get; set; } = Array.Empty<FighterSetup>();
	public AiSetup? Ai { get; set; }
	public List<ReplayEvent> Events { get; set; } = new();
	/// <summary>Informational: the RunState hash when the fight came from a run (YOK-47).</summary>
	public string? RunHash { get; set; }
	public ReplayResult? Result { get; set; }
}

/// <summary>
/// YOK-49: one recorded fight. File (.yfr) = "YFR1" + gzip( int header length, header JSON, int steps,
/// per step: P1 bits u16, P1 move u8, P2 bits u16, P2 move u8, StateHash u64 after the step ).
/// P2 inputs are kept even when the AI drove P2: a replay re-runs the AI and checks they match.
/// </summary>
public sealed class ReplayRecording
{
	public const int FormatVersion = 1;
	static readonly byte[] Magic = "YFR1"u8.ToArray();
	static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

	public ReplayHeader Header { get; set; } = new();
	public List<FighterInput> P1 { get; } = new();
	public List<FighterInput> P2 { get; } = new();
	public List<ulong> Hashes { get; } = new();
	public int Steps => Hashes.Count;

	public byte[] ToBytes()
	{
		using var ms = new MemoryStream();
		ms.Write(Magic);
		using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
		using (var w = new BinaryWriter(gz))
		{
			var header = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Header, Json));
			w.Write(header.Length);
			w.Write(header);
			w.Write(Steps);
			for (int i = 0; i < Steps; i++)
			{
				w.Write((ushort)P1[i].Bits); w.Write(P1[i].Move);
				w.Write((ushort)P2[i].Bits); w.Write(P2[i].Move);
				w.Write(Hashes[i]);
			}
		}
		return ms.ToArray();
	}

	public static ReplayRecording FromBytes(byte[] bytes)
	{
		if (bytes.Length < 4 || !bytes.AsSpan(0, 4).SequenceEqual(Magic)) throw new FormatException("not a Yokai Fighters replay (bad magic)");
		using var ms = new MemoryStream(bytes, 4, bytes.Length - 4);
		using var gz = new GZipStream(ms, CompressionMode.Decompress);
		using var r = new BinaryReader(gz);
		var rec = new ReplayRecording();
		rec.Header = JsonSerializer.Deserialize<ReplayHeader>(r.ReadBytes(r.ReadInt32()), Json) ?? throw new FormatException("empty replay header");
		if (rec.Header.Version != FormatVersion) throw new FormatException($"replay format {rec.Header.Version}, this build reads {FormatVersion}");
		int n = r.ReadInt32();
		for (int i = 0; i < n; i++)
		{
			rec.P1.Add(new FighterInput((InputBits)r.ReadUInt16(), r.ReadByte()));
			rec.P2.Add(new FighterInput((InputBits)r.ReadUInt16(), r.ReadByte()));
			rec.Hashes.Add(r.ReadUInt64());
		}
		return rec;
	}

	public void Save(string path)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
		File.WriteAllBytes(path, ToBytes());
	}

	public static ReplayRecording Load(string path) => FromBytes(File.ReadAllBytes(path));
}

/// <summary>
/// Stable content fingerprint (FNV-1a 64) of a data object: public instance properties in declaration order,
/// recursing into lists and nested records; JsonNode as its JSON text. Same data = same fingerprint across runs.
/// </summary>
public static class Fingerprint
{
	public static string Of(object? o)
	{
		ulong h = 14695981039346656037UL;
		Mix(ref h, o, 0);
		return h.ToString("x16");
	}

	static void Bytes(ref ulong h, string s)
	{
		unchecked
		{
			foreach (char c in s) { h ^= (byte)c; h *= 1099511628211UL; h ^= (byte)(c >> 8); h *= 1099511628211UL; }
			h ^= 0xFF; h *= 1099511628211UL; // terminator
		}
	}

	static void Mix(ref ulong h, object? o, int depth)
	{
		if (depth > 12) throw new InvalidOperationException("fingerprint: data nested too deep");
		switch (o)
		{
			case null: Bytes(ref h, "\u0000null"); return;
			case string s: Bytes(ref h, s); return;
			case JsonNode n: Bytes(ref h, n.ToJsonString()); return;
			case Enum e: Bytes(ref h, Convert.ToInt64(e).ToString()); return;
			case IFormattable f when o.GetType().IsPrimitive: Bytes(ref h, f.ToString(null, System.Globalization.CultureInfo.InvariantCulture)); return;
			case bool b: Bytes(ref h, b ? "T" : "F"); return;
			case IEnumerable list:
				Bytes(ref h, "[");
				foreach (var x in list) Mix(ref h, x, depth + 1);
				Bytes(ref h, "]");
				return;
		}
		var t = o.GetType();
		Bytes(ref h, t.Name);
		var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
		Array.Sort(props, (a, b) => string.CompareOrdinal(a.Name, b.Name)); // reflection order is not guaranteed
		foreach (var p in props)
		{
			if (p.GetIndexParameters().Length > 0) continue;
			Bytes(ref h, p.Name);
			Mix(ref h, p.GetValue(o), depth + 1);
		}
	}
}
