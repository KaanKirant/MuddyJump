using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Reusable settings panel. Attach to a settings panel GameObject in both
/// the main menu canvas and the pause menu canvas — same script, same
/// PlayerPrefs keys, works identically in both contexts.
///
/// Controlled settings:
///   Music Volume    — slider 0→1, saved to PlayerPrefs, applied via SoundManager
///   SFX Volume      — slider 0→1, saved to PlayerPrefs, applied via SoundManager
///   Vibration       — toggle on/off, saved to PlayerPrefs
///   Target FPS      — 30 or 60, saved to PlayerPrefs (battery vs smoothness tradeoff)
///
/// All values are loaded from PlayerPrefs when the panel activates so they
/// always reflect the current saved state regardless of which scene opened them.
///
/// Inspector setup:
///   Assign the four UI controls. Labels are optional — leave null to skip.
///   Call Show() / Hide() from your menu scripts, or toggle the GameObject directly.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    // ─── Music ────────────────────────────────────────────────────────────────
    [Header("Music")]
    [Tooltip("Slider controlling music volume (0-1).")]
    [SerializeField] private Slider musicSlider;
    [Tooltip("Optional label showing current music volume as a percentage.")]
    [SerializeField] private TextMeshProUGUI musicValueLabel;

    // ─── SFX ──────────────────────────────────────────────────────────────────
    [Header("SFX")]
    [Tooltip("Slider controlling SFX volume (0-1).")]
    [SerializeField] private Slider sfxSlider;
    [Tooltip("Optional label showing current SFX volume as a percentage.")]
    [SerializeField] private TextMeshProUGUI sfxValueLabel;

    // ─── Vibration ────────────────────────────────────────────────────────────
    [Header("Vibration")]
    [Tooltip("Toggle for vibration feedback.")]
    [SerializeField] private Toggle vibrationToggle;

    // ─── FPS ──────────────────────────────────────────────────────────────────
    [Header("Target FPS")]
    [Tooltip("Button to select 30 FPS (battery saver).")]
    [SerializeField] private Button fps30Button;
    [Tooltip("Button to select 60 FPS (smooth).")]
    [SerializeField] private Button fps60Button;
    [Tooltip("Highlight color on the active FPS button.")]
    [SerializeField] private Color activeFPSColor = new Color(1f, 0.8f, 0.2f);
    [Tooltip("Default color on the inactive FPS button.")]
    [SerializeField] private Color inactiveFPSColor = new Color(0.4f, 0.4f, 0.4f);

    // ─── Close ────────────────────────────────────────────────────────────────
    [Header("Close")]
    [Tooltip("Button that hides this panel. Wire to Hide() or assign in Inspector.")]
    [SerializeField] private Button closeButton;

    // ─── PlayerPrefs Keys ─────────────────────────────────────────────────────
    private const string MusicVolumeKey = "MUSIC_VOLUME";
    private const string SFXVolumeKey = "SFX_VOLUME";
    private const string VibrationKey = "VIBRATION";
    private const string TargetFPSKey = "TARGET_FPS";

    private const int DefaultFPS = 60;

    // ─── State ────────────────────────────────────────────────────────────────
    private bool _initialising;   // Suppresses callbacks during LoadSettings

    #region Unity Lifecycle

    private void Awake()
    {
        // Wire close button
        closeButton?.onClick.AddListener(Hide);

        // Wire FPS buttons
        fps30Button?.onClick.AddListener(() => SetTargetFPS(30));
        fps60Button?.onClick.AddListener(() => SetTargetFPS(60));

        // Wire sliders — callbacks fire when value changes
        musicSlider?.onValueChanged.AddListener(OnMusicSliderChanged);
        sfxSlider?.onValueChanged.AddListener(OnSFXSliderChanged);

        // Wire vibration toggle
        vibrationToggle?.onValueChanged.AddListener(OnVibrationToggleChanged);
    }

    private void OnEnable()
    {
        // Reload from PlayerPrefs every time the panel becomes visible
        // so it always reflects the latest saved state
        LoadSettings();
    }

    private void OnDestroy()
    {
        closeButton?.onClick.RemoveAllListeners();
        fps30Button?.onClick.RemoveAllListeners();
        fps60Button?.onClick.RemoveAllListeners();
        musicSlider?.onValueChanged.RemoveAllListeners();
        sfxSlider?.onValueChanged.RemoveAllListeners();
        vibrationToggle?.onValueChanged.RemoveAllListeners();
    }

    #endregion

    #region Load / Save

    private void LoadSettings()
    {
        _initialising = true;

        // Music volume
        float music = PlayerPrefs.GetFloat(MusicVolumeKey, 0.6f);
        if (musicSlider != null) musicSlider.value = music;
        UpdateMusicLabel(music);

        // SFX volume
        float sfx = PlayerPrefs.GetFloat(SFXVolumeKey, 1f);
        if (sfxSlider != null) sfxSlider.value = sfx;
        UpdateSFXLabel(sfx);

        // Vibration
        bool vibration = PlayerPrefs.GetInt(VibrationKey, 1) == 1;
        if (vibrationToggle != null) vibrationToggle.isOn = vibration;

        // Target FPS
        int fps = PlayerPrefs.GetInt(TargetFPSKey, DefaultFPS);
        ApplyFPS(fps);
        UpdateFPSButtons(fps);

        _initialising = false;
    }

    #endregion

    #region Music

    private void OnMusicSliderChanged(float value)
    {
        if (_initialising) return;

        SoundManager.Instance?.SetMusicVolume(value);
        // PlayerPrefs saved inside SoundManager.SetMusicVolume
        UpdateMusicLabel(value);
        SoundManager.Instance?.PlaySFX(SoundType.UIClick);
    }

    private void UpdateMusicLabel(float value)
    {
        if (musicValueLabel != null)
            musicValueLabel.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    #endregion

    #region SFX

    private void OnSFXSliderChanged(float value)
    {
        if (_initialising) return;

        SoundManager.Instance?.SetSFXVolume(value);
        // PlayerPrefs saved inside SoundManager.SetSFXVolume
        UpdateSFXLabel(value);
        SoundManager.Instance?.PlaySFX(SoundType.UIClick);
    }

    private void UpdateSFXLabel(float value)
    {
        if (sfxValueLabel != null)
            sfxValueLabel.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    #endregion

    #region Vibration

    private void OnVibrationToggleChanged(bool isOn)
    {
        if (_initialising) return;

        PlayerPrefs.SetInt(VibrationKey, isOn ? 1 : 0);
        PlayerPrefs.Save();
        SoundManager.Instance?.PlaySFX(SoundType.UIClick);
    }

    /// <summary>
    /// Call this before triggering any haptic feedback.
    /// Returns false if the player has vibration disabled.
    /// </summary>
    public static bool IsVibrationEnabled()
    {
        return PlayerPrefs.GetInt(VibrationKey, 1) == 1;
    }

    #endregion

    #region Target FPS

    private void SetTargetFPS(int fps)
    {
        ApplyFPS(fps);
        UpdateFPSButtons(fps);
        PlayerPrefs.SetInt(TargetFPSKey, fps);
        PlayerPrefs.Save();
        SoundManager.Instance?.PlaySFX(SoundType.UIClick);
    }

    private void ApplyFPS(int fps)
    {
        Application.targetFrameRate = fps;
        QualitySettings.vSyncCount = 0;   // vSync must be off for targetFrameRate to work
    }

    private void UpdateFPSButtons(int activeFPS)
    {
        SetButtonColor(fps30Button, activeFPS == 30 ? activeFPSColor : inactiveFPSColor);
        SetButtonColor(fps60Button, activeFPS == 60 ? activeFPSColor : inactiveFPSColor);
    }

    private void SetButtonColor(Button btn, Color color)
    {
        if (btn == null) return;
        Image img = btn.GetComponent<Image>();
        if (img != null) img.color = color;
    }

    #endregion

    #region Public API

    /// <summary>Shows the settings panel.</summary>
    public void Show()
    {
        SoundManager.Instance?.PlaySFX(SoundType.UIClick);
        gameObject.SetActive(true);
    }

    /// <summary>Hides the settings panel.</summary>
    public void Hide()
    {
        SoundManager.Instance?.PlaySFX(SoundType.UIClick);
        gameObject.SetActive(false);
    }

    #endregion
}