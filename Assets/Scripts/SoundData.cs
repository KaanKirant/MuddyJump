using System;
using UnityEngine;

/// <summary>
/// ScriptableObject that maps every SoundType and MusicType to an AudioClip.
/// Create one asset via Right-click > Audio > Sound Data and assign it to SoundManager.
///
/// Each entry carries its own volume so loud and quiet sounds can be balanced
/// without touching code — just drag clips in and adjust the sliders.
/// </summary>
[CreateAssetMenu(fileName = "SoundData", menuName = "Audio/Sound Data")]
public class SoundData : ScriptableObject
{
    [Header("SFX")]
    public SoundEntry[] sfxEntries;

    [Header("Music")]
    public MusicEntry[] musicEntries;

    /// <summary>Returns the SoundEntry for the given SoundType, or null if not found.</summary>
    public SoundEntry GetSFX(SoundType type)
    {
        foreach (SoundEntry e in sfxEntries)
            if (e.type == type) return e;

        Debug.LogWarning($"[SoundData] No entry found for SoundType.{type}");
        return null;
    }

    /// <summary>Returns the MusicEntry for the given MusicType, or null if not found.</summary>
    public MusicEntry GetMusic(MusicType type)
    {
        foreach (MusicEntry e in musicEntries)
            if (e.type == type) return e;

        Debug.LogWarning($"[SoundData] No entry found for MusicType.{type}");
        return null;
    }
}

/// <summary>One SFX entry — maps a SoundType to a clip with volume and optional pitch variance.</summary>
[Serializable]
public class SoundEntry
{
    public SoundType type;

    [Tooltip("Audio clip to play.")]
    public AudioClip clip;

    [Range(0f, 1f)]
    [Tooltip("Playback volume for this sound.")]
    public float volume = 1f;

    [Range(-3f, 3f)]
    [Tooltip("Base pitch. Add PitchVariance for slight randomisation.")]
    public float pitch = 1f;

    [Range(0f, 0.5f)]
    [Tooltip("Random pitch variance added each play. 0 = no variance.")]
    public float pitchVariance = 0f;
}

/// <summary>One music entry — maps a MusicType to a looping clip with volume.</summary>
[Serializable]
public class MusicEntry
{
    public MusicType type;

    [Tooltip("Music clip to loop.")]
    public AudioClip clip;

    [Range(0f, 1f)]
    public float volume = 0.6f;
}