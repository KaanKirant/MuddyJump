using System.Collections;
using UnityEngine;

/// <summary>
/// Central audio manager. Persists across scenes via DontDestroyOnLoad.
///
/// Two dedicated AudioSources on this GameObject:
///   _musicSource  — looping, for background music
///   _sfxSource    — one-shot, for non-overlapping SFX
///
/// A small pool of pooled AudioSources handles overlapping SFX (e.g. rapid
/// kicks in quick succession). Pool size is configurable in the Inspector.
///
/// Call from anywhere:
///   SoundManager.Instance.PlaySFX(SoundType.KickSuccess);
///   SoundManager.Instance.PlayMusic(MusicType.Gameplay);
///
/// Volume is saved to PlayerPrefs and restored on start.
/// </summary>
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    // ─── Data ─────────────────────────────────────────────────────────────────
    [Header("Sound Data")]
    [Tooltip("ScriptableObject mapping SoundType/MusicType enums to AudioClips. " +
             "Create via Right-click > Audio > Sound Data.")]
    public SoundData soundData;

    // ─── Pool ─────────────────────────────────────────────────────────────────
    [Header("SFX Pool")]
    [Tooltip("Number of pooled AudioSources for overlapping SFX. " +
             "Increase if rapid sounds get cut off.")]
    public int sfxPoolSize = 6;

    // ─── Volume ───────────────────────────────────────────────────────────────
    [Header("Default Volumes")]
    [Range(0f, 1f)] public float defaultMusicVolume = 0.6f;
    [Range(0f, 1f)] public float defaultSFXVolume = 1f;

    // ─── Music Crossfade ──────────────────────────────────────────────────────
    [Header("Music")]
    [Tooltip("Duration in seconds to fade between music tracks.")]
    public float musicFadeDuration = 1f;

    // ─── Private ──────────────────────────────────────────────────────────────
    private AudioSource _musicSource;
    private AudioSource[] _sfxPool;
    private int _sfxPoolIndex;

    private float _musicVolume;
    private float _sfxVolume;

    private Coroutine _musicFadeRoutine;

    private const string MusicVolumeKey = "MUSIC_VOLUME";
    private const string SFXVolumeKey = "SFX_VOLUME";

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildAudioSources();
        LoadVolumes();
    }

    #endregion

    #region Setup

    private void BuildAudioSources()
    {
        // Dedicated music source
        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.loop = true;
        _musicSource.volume = defaultMusicVolume;

        // SFX pool — round-robin so overlapping sounds don't cut each other off
        _sfxPool = new AudioSource[sfxPoolSize];
        for (int i = 0; i < sfxPoolSize; i++)
        {
            _sfxPool[i] = gameObject.AddComponent<AudioSource>();
            _sfxPool[i].loop = false;
            _sfxPool[i].volume = defaultSFXVolume;
        }
    }

    private void LoadVolumes()
    {
        _musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, defaultMusicVolume);
        _sfxVolume = PlayerPrefs.GetFloat(SFXVolumeKey, defaultSFXVolume);

        _musicSource.volume = _musicVolume;
        foreach (AudioSource src in _sfxPool)
            src.volume = _sfxVolume;
    }

    #endregion

    #region Public API — SFX

    /// <summary>
    /// Plays a one-shot SFX by type. Uses the round-robin pool so
    /// overlapping sounds (rapid kicks, hits) all play simultaneously.
    /// Does nothing if the clip is not assigned in SoundData.
    /// </summary>
    public void PlaySFX(SoundType type)
    {
        if (soundData == null) return;

        SoundEntry entry = soundData.GetSFX(type);
        if (entry?.clip == null) return;

        AudioSource src = GetNextPooledSource();
        src.clip = entry.clip;
        src.volume = entry.volume * _sfxVolume;
        src.pitch = entry.pitch + Random.Range(-entry.pitchVariance, entry.pitchVariance);
        src.Play();
    }

    /// <summary>Plays a SFX at a specific world position (uses PlayClipAtPoint — no pool).</summary>
    public void PlaySFXAtPoint(SoundType type, Vector3 position)
    {
        if (soundData == null) return;

        SoundEntry entry = soundData.GetSFX(type);
        if (entry?.clip == null) return;

        AudioSource.PlayClipAtPoint(entry.clip, position, entry.volume * _sfxVolume);
    }

    #endregion

    #region Public API — Music

    /// <summary>
    /// Switches to a new music track with a crossfade.
    /// If the same track is already playing, does nothing.
    /// </summary>
    public void PlayMusic(MusicType type)
    {
        if (soundData == null) return;

        MusicEntry entry = soundData.GetMusic(type);
        if (entry?.clip == null) return;

        // Already playing this track — don't restart
        if (_musicSource.clip == entry.clip && _musicSource.isPlaying) return;

        if (_musicFadeRoutine != null) StopCoroutine(_musicFadeRoutine);
        _musicFadeRoutine = StartCoroutine(CrossfadeMusic(entry));
    }

    /// <summary>Stops music with a fade out.</summary>
    public void StopMusic()
    {
        if (_musicFadeRoutine != null) StopCoroutine(_musicFadeRoutine);
        _musicFadeRoutine = StartCoroutine(FadeOutMusic());
    }

    #endregion

    #region Public API — Volume

    public float MusicVolume => _musicVolume;
    public float SFXVolume => _sfxVolume;

    /// <summary>Sets music volume and saves to PlayerPrefs.</summary>
    public void SetMusicVolume(float volume)
    {
        _musicVolume = Mathf.Clamp01(volume);
        _musicSource.volume = _musicVolume;
        PlayerPrefs.SetFloat(MusicVolumeKey, _musicVolume);
        PlayerPrefs.Save();
    }

    /// <summary>Sets SFX volume and saves to PlayerPrefs.</summary>
    public void SetSFXVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
        foreach (AudioSource src in _sfxPool)
            src.volume = _sfxVolume;
        PlayerPrefs.SetFloat(SFXVolumeKey, _sfxVolume);
        PlayerPrefs.Save();
    }

    #endregion

    #region Helpers

    private AudioSource GetNextPooledSource()
    {
        AudioSource src = _sfxPool[_sfxPoolIndex];
        _sfxPoolIndex = (_sfxPoolIndex + 1) % _sfxPool.Length;
        return src;
    }

    #endregion

    #region Coroutines

    private IEnumerator CrossfadeMusic(MusicEntry entry)
    {
        // Fade out current track
        float startVolume = _musicSource.volume;
        float elapsed = 0f;

        while (elapsed < musicFadeDuration * 0.5f)
        {
            elapsed += Time.unscaledDeltaTime;
            _musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / (musicFadeDuration * 0.5f));
            yield return null;
        }

        // Swap clip and fade in
        _musicSource.clip = entry.clip;
        _musicSource.volume = 0f;
        _musicSource.Play();

        elapsed = 0f;
        float targetVolume = entry.volume * _musicVolume;

        while (elapsed < musicFadeDuration * 0.5f)
        {
            elapsed += Time.unscaledDeltaTime;
            _musicSource.volume = Mathf.Lerp(0f, targetVolume, elapsed / (musicFadeDuration * 0.5f));
            yield return null;
        }

        _musicSource.volume = targetVolume;
    }

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
        _musicSource.volume = _musicVolume;
    }

    #endregion
}