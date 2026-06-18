using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject mapping every SoundType and MusicType to an AudioClip.
/// Create one asset via Right-click > Audio > Sound Data, assign it to SoundManager.
///
/// Each entry carries its own volume and pitch so sounds can be balanced
/// without touching code — drag clips in and adjust sliders in the Inspector.
///
/// Lookup performance:
///   Entries are stored in arrays for easy Inspector editing.
///   At runtime, Awake() on SoundManager could build a Dictionary if the
///   entry count grows large (>~20) and profiling shows GetSFX() as a hotspot.
///   For typical hypercasual projects, linear search across <20 entries is fine.
/// </summary>
[CreateAssetMenu(fileName = "SoundData", menuName = "Audio/Sound Data")]
public class SoundData : ScriptableObject
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("SFX")]
    [Tooltip("One entry per SoundType. Order does not matter — looked up by type enum.")]
    public SoundEntry[] sfxEntries;

    [Header("Music")]
    [Tooltip("One entry per MusicType. Order does not matter — looked up by type enum.")]
    public MusicEntry[] musicEntries;

    // ─── Runtime Caches ───────────────────────────────────────────────────────
    // Built on first access so ScriptableObjects don't pay dictionary overhead
    // in projects that never call GetSFX() (e.g. editor tooling, tests).
    private Dictionary<SoundType, SoundEntry> _sfxCache;
    private Dictionary<MusicType, MusicEntry> _musicCache;

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the SoundEntry for the given SoundType, or null if not found.
    /// Builds a lookup cache on first call — O(1) for every subsequent call.
    /// </summary>
    public SoundEntry GetSFX(SoundType type)
    {
        BuildSFXCacheIfNeeded();
        if (_sfxCache.TryGetValue(type, out SoundEntry entry)) return entry;

        Debug.LogWarning($"[SoundData] No entry found for SoundType.{type}");
        return null;
    }

    /// <summary>
    /// Returns the MusicEntry for the given MusicType, or null if not found.
    /// Builds a lookup cache on first call — O(1) for every subsequent call.
    /// </summary>
    public MusicEntry GetMusic(MusicType type)
    {
        BuildMusicCacheIfNeeded();
        if (_musicCache.TryGetValue(type, out MusicEntry entry)) return entry;

        Debug.LogWarning($"[SoundData] No entry found for MusicType.{type}");
        return null;
    }

    // ─── Cache Helpers ────────────────────────────────────────────────────────

    private void BuildSFXCacheIfNeeded()
    {
        if (_sfxCache != null) return;
        _sfxCache = new Dictionary<SoundType, SoundEntry>(sfxEntries?.Length ?? 0);
        if (sfxEntries == null) return;
        foreach (SoundEntry e in sfxEntries)
            _sfxCache[e.type] = e;
    }

    private void BuildMusicCacheIfNeeded()
    {
        if (_musicCache != null) return;
        _musicCache = new Dictionary<MusicType, MusicEntry>(musicEntries?.Length ?? 0);
        if (musicEntries == null) return;
        foreach (MusicEntry e in musicEntries)
            _musicCache[e.type] = e;
    }

    // ─── ScriptableObject Lifecycle ───────────────────────────────────────────

    /// <summary>
    /// Invalidate caches when the asset is modified in the Editor so hot-reloads
    /// pick up changes without requiring a domain reload.
    /// </summary>
    private void OnValidate()
    {
        _sfxCache = null;
        _musicCache = null;
    }
}

/// <summary>
/// One SFX entry: maps a SoundType to an AudioClip with per-clip volume and pitch settings.
/// </summary>
[Serializable]
public class SoundEntry
{
    [Tooltip("The SoundType this entry responds to.")]
    public SoundType type;

    [Tooltip("AudioClip to play when this type is requested.")]
    public AudioClip clip;

    [Range(0f, 1f)]
    [Tooltip("Playback volume. Multiplied by the master SFX volume at runtime.")]
    public float volume = 1f;

    [Range(0.1f, 3f)]
    [Tooltip("Base pitch. 1 = normal speed. Adjust per-clip for size/weight impression.")]
    public float pitch = 1f;

    [Range(0f, 0.5f)]
    [Tooltip("Random ±pitch variance applied each play. 0 = no randomisation.")]
    public float pitchVariance = 0f;
}

/// <summary>
/// One music entry: maps a MusicType to a looping AudioClip with a volume level.
/// </summary>
[Serializable]
public class MusicEntry
{
    [Tooltip("The MusicType this entry responds to.")]
    public MusicType type;

    [Tooltip("Looping music clip.")]
    public AudioClip clip;

    [Range(0f, 1f)]
    [Tooltip("Target volume for this track. Multiplied by the master music volume.")]
    public float volume = 0.6f;
}