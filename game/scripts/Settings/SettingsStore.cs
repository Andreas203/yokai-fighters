using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using YokaiFighters.Sim;

namespace YokaiFighters.Settings;

public enum SettingsLoadStatus
{
	/// <summary>Current-version file, every field valid.</summary>
	Loaded,
	/// <summary>No file: defaults (a first launch).</summary>
	Missing,
	/// <summary>Not JSON, not an object, or unreadable: defaults.</summary>
	Corrupt,
	/// <summary>Readable current-version file with missing or invalid fields: those fields are defaults, the rest kept.</summary>
	Partial,
	/// <summary>Written by an older build (or no version): valid fields kept, rewritten as the current version on the next save.</summary>
	OlderVersion,
	/// <summary>Written by a newer build: the fields this build knows are kept.</summary>
	NewerVersion,
}

/// <summary>What <see cref="SettingsStore.Load"/> found. <see cref="Data"/> is always usable.</summary>
public sealed record SettingsLoad(SettingsData Data, SettingsLoadStatus Status, IReadOnlyList<string> Problems);

/// <summary>
/// Reads and writes the settings file. Pure C# (no Godot) with the path injected, so tests use a temp file and never
/// the player's; the game passes the globalized <c>user://settings.json</c> (<see cref="FileName"/>).
/// <para>
/// Format (version <see cref="CurrentVersion"/>), one flat JSON object:
/// <c>{ "version": 1, "master": 80, "music": 70, "effects": 80, "muted": false, "scheme": "kihon" }</c>.
/// Loading never throws and never loses a valid field: each field is read on its own, a missing or wrong-typed one
/// takes its default, levels outside 0-100 are clamped, unknown fields are ignored. Saving writes a temp file and
/// renames it over the old one, so a crash mid-write cannot leave half a file.
/// </para>
/// </summary>
public sealed class SettingsStore
{
	public const int CurrentVersion = 1;
	public const string FileName = "settings.json";

	public string Path { get; }
	/// <summary>Why the last <see cref="Save"/> failed; null after a good one.</summary>
	public string? LastSaveError { get; private set; }

	public SettingsStore(string path) => Path = path ?? throw new ArgumentNullException(nameof(path));

	public SettingsLoad Load()
	{
		string text;
		try
		{
			if (!File.Exists(Path)) return new SettingsLoad(SettingsData.Defaults, SettingsLoadStatus.Missing, Array.Empty<string>());
			text = File.ReadAllText(Path);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
		{
			return new SettingsLoad(SettingsData.Defaults, SettingsLoadStatus.Corrupt, new[] { "cannot read: " + e.Message });
		}
		return Parse(text);
	}

	/// <summary>The file's text to settings (what <see cref="Load"/> does after reading).</summary>
	public static SettingsLoad Parse(string text)
	{
		JsonDocument doc;
		try { doc = JsonDocument.Parse(text, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }); }
		catch (JsonException e) { return new SettingsLoad(SettingsData.Defaults, SettingsLoadStatus.Corrupt, new[] { "not JSON: " + e.Message }); }
		using (doc)
		{
			JsonElement root = doc.RootElement;
			if (root.ValueKind != JsonValueKind.Object)
				return new SettingsLoad(SettingsData.Defaults, SettingsLoadStatus.Corrupt, new[] { "not a JSON object" });

			var problems = new List<string>();
			var d = SettingsData.Defaults;
			int version = 0; // a file without a version is older than version 1
			if (root.TryGetProperty("version", out JsonElement v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n)) version = n;

			// Version 1 is the first format, so there is nothing to convert yet: an older or versionless file is read
			// with the same field names. A later format change adds its conversion here, keyed on `version`.
			d = d with
			{
				Master = Level(root, "master", d.Master, problems),
				Music = Level(root, "music", d.Music, problems),
				Effects = Level(root, "effects", d.Effects, problems),
				Muted = Flag(root, "muted", d.Muted, problems),
				Scheme = Scheme(root, "scheme", d.Scheme, problems),
			};
			SettingsLoadStatus status =
				version < CurrentVersion ? SettingsLoadStatus.OlderVersion :
				version > CurrentVersion ? SettingsLoadStatus.NewerVersion :
				problems.Count > 0 ? SettingsLoadStatus.Partial : SettingsLoadStatus.Loaded;
			if (version != CurrentVersion) problems.Insert(0, $"version {version}, this build writes {CurrentVersion}");
			return new SettingsLoad(d, status, problems);
		}
	}

	private static int Level(JsonElement root, string name, int fallback, List<string> problems)
	{
		if (!root.TryGetProperty(name, out JsonElement e)) { problems.Add($"{name}: missing, using {fallback}"); return fallback; }
		if (e.ValueKind != JsonValueKind.Number || !e.TryGetDouble(out double value) || double.IsNaN(value))
		{
			problems.Add($"{name}: not a number, using {fallback}");
			return fallback;
		}
		int level = (int)Math.Round(Math.Clamp(value, SettingsData.MinLevel, SettingsData.MaxLevel));
		if (level != value) problems.Add($"{name}: {e.GetRawText()} taken as {level}");
		return level;
	}

	private static bool Flag(JsonElement root, string name, bool fallback, List<string> problems)
	{
		if (!root.TryGetProperty(name, out JsonElement e)) { problems.Add($"{name}: missing, using {fallback}"); return fallback; }
		if (e.ValueKind == JsonValueKind.True) return true;
		if (e.ValueKind == JsonValueKind.False) return false;
		problems.Add($"{name}: not true/false, using {fallback}");
		return fallback;
	}

	private static ControlScheme Scheme(JsonElement root, string name, ControlScheme fallback, List<string> problems)
	{
		if (!root.TryGetProperty(name, out JsonElement e)) { problems.Add($"{name}: missing, using {fallback}"); return fallback; }
		if (e.ValueKind == JsonValueKind.String)
			foreach (ControlScheme s in Enum.GetValues<ControlScheme>())
				if (string.Equals(e.GetString()?.Trim(), s.ToString(), StringComparison.OrdinalIgnoreCase)) return s;
		problems.Add($"{name}: {e.GetRawText()} is not a scheme, using {fallback}");
		return fallback;
	}

	/// <summary>The file's text for these settings (current version, levels clamped).</summary>
	public static string Serialize(SettingsData d)
	{
		using var stream = new MemoryStream();
		using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
		{
			w.WriteStartObject();
			w.WriteNumber("version", CurrentVersion);
			w.WriteNumber("master", SettingsData.ClampLevel(d.Master));
			w.WriteNumber("music", SettingsData.ClampLevel(d.Music));
			w.WriteNumber("effects", SettingsData.ClampLevel(d.Effects));
			w.WriteBoolean("muted", d.Muted);
			w.WriteString("scheme", d.Scheme.ToString().ToLowerInvariant());
			w.WriteEndObject();
		}
		return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
	}

	/// <summary>Writes the file (temp file, then rename). False with <see cref="LastSaveError"/> when the disk refuses; never throws.</summary>
	public bool Save(SettingsData data)
	{
		string temp = Path + ".tmp";
		try
		{
			string? dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path));
			if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
			File.WriteAllText(temp, Serialize(data), new UTF8Encoding(false));
			File.Move(temp, Path, overwrite: true);
			LastSaveError = null;
			return true;
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException or ArgumentException)
		{
			LastSaveError = e.Message;
			try { if (File.Exists(temp)) File.Delete(temp); } catch (Exception) { /* nothing more to do */ }
			return false;
		}
	}
}
