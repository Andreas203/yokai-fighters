namespace YokaiFighters.Sim;

/// <summary>
/// Per-player control scheme (K1 Kata, K2 Kihon). Only the parser differs (K3); switching mid-session
/// swaps the parser and keeps the raw history and any waiting command, so nothing else changes.
/// </summary>
public enum ControlScheme : byte { Kata, Kihon }
