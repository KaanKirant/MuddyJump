using System.Collections;
using UnityEngine;

/// <summary>
/// Central audio manager. Persists across scenes via DontDestroyOnLoad.
///
/// Audio source layout (all on this GameObject):
///   _musicSource   — single looping source for background music
///   _sfxPool[]     — round-robin pool for overlapping one-shot SFX
///
/// The round-robin pool prevents rapid sounds (kicks, hits) from cutting
/// each other off. Pool size is tunable in the Inspector.
///
/// Usage from any script:
///   SoundManager.Instance?.PlaySFX(SoundType.KickSuccess);
///   SoundManager.Instance?.PlayMusic(MusicType.Gameplay);
///
/// Volume is persisted to PlayerPrefs and restored on Awake.
/// SettingsPanel calls SetMusicVolume / SetSFXVolume which handle persistence.
/// </summary>
public class SoundManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static SoundManager Instance { get; private set; }

    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Sound Data")]
    [Tooltip("ScriptableObject mapping SoundType/MusicType to AudioClips. " +
             "Create via Right-click > Audio > Sound Data.")]
    public SoundData soundData;

    [Header("SFX Pool")]
    [Tooltip("Number of pooled AudioSources for overlapping SFX. " +
             "Increase if rapid sounds get cut off (each kick + hit fires in the same frame).")]
    public int sfxPoolSize = 8;

    [Header("Default Volumes")]
    [Range(0f, 1f)] public float defaultMusicVolume = 0.6f;
    [Range(0f, 1f)] public float defaultSFXVolume = 1f;

    [Header("Music")]
    [Tooltip("Seconds for a full crossfade between tracks (half used for fade-out, half for fade-in).")]
    public float musicFadeDuration = 1f;

    // ─── Private ──────────────────────────────────────────────────────────────
    private AudioSource _musicSource;
    private AudioSource[] _sfxPool;
    private int _sfxPoolIndex;

    private float _musicVolume;
    private float _sfxVolume;

    private Coroutine _musicFadeRoutine;

    // Keys must match SettingsPanel's constants exactly.
    private const string MusicVolumeKey = "MUSIC_VOLUME";
    private const string SFXVolumeKey = "SFX_VOLUME";

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildAudioSources();
        LoadVolumes();
    }

    // ─── Setup ────────────────────────────────────────────────────────────────

    private void BuildAudioSources()
    {
        // One dedicated looping source for music.
        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.loop = true;
        _musicSource.volume = defaultMusicVolume;

        // Pool of one-shot sources — round-robin prevents mutual interruption.
        _sfxPool = new AudioSource[sfxPoolSize];
        for (int i = 0; i < sfxPoolSize; i++)
        {
            _sfxPool[i] = gameObject.AddComponent<AudioSource>();
            _sfxPool[i].loop = false;
            _sfxPool[i].volume = defaultSFXVolume;
        }
    }

    /// <summary>
    /// Loads persisted volume values from PlayerPrefs and applies them to all sources.
    /// Called once in Awake — SettingsPanel keeps values updated during the session.
    /// </summary>
    private void LoadVolumes()
    {
        _musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, defaultMusicVolume);
        _sfxVolume = PlayerPrefs.GetFloat(SFXVolumeKey, defaultSFXVolume);
        _musicSource.volume = _musicVolume;

        foreach (AudioSource src in _sfxPool)
            src.volume = _sfxVolume;
    }

    // ─── Public API — SFX ─────────────────────────────────────────────────────

    /// <summary>
    /// Plays a one-shot SFX via the round-robin pool.
    /// Overlapping sounds (e.g. rapid kicks) all play simultaneously.
    /// No-ops silently when the clip is not assigned in SoundData.
    /// </summary>
    public void PlaySFX(SoundType type)
    {
        if (soundData == null) return;

        SoundEntry entry = soundData.GetSFX(type);
        if (entry?.clip == null) return;

        AudioSource src = GetNextPooledSource();
        src.clip = entry.clip;
        src.volume = entry.volume * _sfxVolume;
        src.pitch = Mathf.Max(entry.pitch + Random.Range(-entry.pitchVariance, entry.pitchVariance),0.01f);  // never allow 0 — silences the AudioSource

        src.Play();
    }

    /// <summary>
    /// Plays a one-shot SFX at a world position using Unity's PlayClipAtPoint.
    /// Bypasses the pool — useful for positional audio on short, non-overlapping clips.
    /// </summary>
    public void PlaySFXAtPoint(SoundType type, Vector3 position)
    {
        if (soundData == null) return;

        SoundEntry entry = soundData.GetSFX(type);
        if (entry?.clip == null) return;

        AudioSource.PlayClipAtPoint(entry.clip, position, entry.volume * _sfxVolume);
    }

    // ─── Public API — Music ───────────────────────────────────────────────────

    /// <summary>
    /// Switches to a new music track with a crossfade.
    /// No-ops if the requested track is already playing.
    /// </summary>
    public void PlayMusic(MusicType type)
    {
        if (soundData == null) return;

        MusicEntry entry = soundData.GetMusic(type);
        if (entry?.clip == null) return;

        // Same track already playing — don't restart or re-fade.
        if (_musicSource.clip == entry.clip && _musicSource.isPlaying) return;

        if (_musicFadeRoutine != null) StopCoroutine(_musicFadeRoutine);
        _musicFadeRoutine = StartCoroutine(CrossfadeMusic(entry));
    }

    /// <summary>Fades out and stops the current music track.</summary>
    public void StopMusic()
    {
        if (_musicFadeRoutine != null) StopCoroutine(_musicFadeRoutine);
        _musicFadeRoutine = StartCoroutine(FadeOutMusic());
    }

    // ─── Public API — Volume ──────────────────────────────────────────────────

    public float MusicVolume => _musicVolume;
    public float SFXVolume => _sfxVolume;

    /// <summary>
    /// Sets music volume, applies it to the music source, and persists to PlayerPrefs.
    /// Called by SettingsPanel's music slider.
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        _musicVolume = Mathf.Clamp01(volume);
        _musicSource.volume = _musicVolume;
        PlayerPrefs.SetFloat(MusicVolumeKey, _musicVolume);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Sets SFX volume, applies it to all pooled sources (including any currently playing),
    /// and persists to PlayerPrefs. Called by SettingsPanel's SFX slider.
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);

        // Update all sources — including ones mid-playback so the change is immediate.
        foreach (AudioSource src in _sfxPool)
            src.volume = _sfxVolume;

        PlayerPrefs.SetFloat(SFXVolumeKey, _sfxVolume);
        PlayerPrefs.Save();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the next AudioSource in the round-robin pool.
    /// If the source is already playing it will be interrupted — increase
    /// sfxPoolSize if this happens frequently in profiling.
    /// </summary>
    private AudioSource GetNextPooledSource()
    {
        AudioSource src = _sfxPool[_sfxPoolIndex];
        _sfxPoolIndex = (_sfxPoolIndex + 1) % _sfxPool.Length;
        return src;
    }

    // ─── Coroutines ───────────────────────────────────────────────────────────

    /// <summary>
    /// Two-phase crossfade: fade current track to 0, swap clip, fade in the new track.
    /// Uses unscaledDeltaTime so music transitions work correctly during hit-stop and pause.
    /// </summary>
    private IEnumerator CrossfadeMusic(MusicEntry entry)
    {
        float halfDuration = musicFadeDuration * 0.5f;

        // Phase 1: fade out current track.
        float startVolume = _musicSource.volume;
        float elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / halfDuration);
            yield return null;
        }

        // Phase 2: swap clip and fade in.
        _musicSource.clip = entry.clip;
        _musicSource.volume = 0f;
        _musicSource.Play();

        float targetVolume = entry.volume * _musicVolume;
        elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _musicSource.volume = Mathf.Lerp(0f, targetVolume, elapsed / halfDuration);
            yield return null;
        }

        _musicSource.volume = targetVolume;
    }

    /// <summary>
    /// Fades the current track to silence then stops the AudioSource.
    /// Restores _musicVolume on the source afterward so a subsequent PlayMusic
    /// call doesn't start at 0.
    /// Uses unscaledDeltaTime so it works during pause / hit-stop.
    /// </summary>
    private IEnumerator FadeOutMusic()
    {
        float startVolume = _musicSource.volume;
        float elapsed = 0f;

        while (elapsed < musicFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / musicFadeDuration);
            yield return null;
        }

        _musicSource.Stop();
        // Restore baseline volume so the next PlayMusic fades in from the correct target.
        _musicSource.volume = _musicVolume;
    }
}