using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Reusable settings panel. Attach to a settings panel GameObject in both
/// the main-menu canvas and the pause-menu canvas — same script, same
/// PlayerPrefs keys, identical behaviour in both contexts.
///
/// Controlled settings:
///   Music Volume  — slider 0→1, persisted via SoundManager (which saves to PlayerPrefs)
///   SFX Volume    — slider 0→1, persisted via SoundManager
///   Vibration     — toggle on/off, saved directly to PlayerPrefs
///   Target FPS    — 30 or 60, saved to PlayerPrefs (battery vs. smoothness)
///
/// Values are loaded from PlayerPrefs every time OnEnable fires so the panel
/// always reflects the current saved state, regardless of which scene opened it.
///
/// Inspector setup:
///   Wire the four UI controls (sliders, toggle, buttons). Labels are optional.
///   Call Show() / Hide() from menu scripts, or toggle the GameObject directly.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    // ─── Music ────────────────────────────────────────────────────────────────
    [Header("Music")]
    [Tooltip("Slider controlling music volume (0–1).")]
    [SerializeField] private Slider musicSlider;

    [Tooltip("Optional label showing current music volume as a percentage.")]
    [SerializeField] private TextMeshProUGUI musicValueLabel;

    // ─── SFX ──────────────────────────────────────────────────────────────────
    [Header("SFX")]
    [Tooltip("Slider controlling SFX volume (0–1).")]
    [SerializeField] private Slider sfxSlider;

    [Tooltip("Optional label showing current SFX volume as a percentage.")]
    [SerializeField] private TextMeshProUGUI sfxValueLabel;

    // ─── Vibration ────────────────────────────────────────────────────────────
    [Header("Vibration")]
    [Tooltip("Toggle for haptic / vibration feedback.")]
    [SerializeField] private Toggle vibrationToggle;

    // ─── FPS ──────────────────────────────────────────────────────────────────
    [Header("Target FPS")]
    [Tooltip("Button that sets 30 FPS (battery saver).")]
    [SerializeField] private Button fps30Button;

    [Tooltip("Button that sets 60 FPS (smooth).")]
    [SerializeField] private Button fps60Button;

    [Tooltip("Tint applied to the currently active FPS button.")]
    [SerializeField] private Color activeFPSColor = new Color(1f, 0.8f, 0.2f);

    [Tooltip("Tint applied to the inactive FPS button.")]
    [SerializeField] private Color inactiveFPSColor = new Color(0.4f, 0.4f, 0.4f);

    // ─── Close ────────────────────────────────────────────────────────────────
    [Header("Close")]
    [Tooltip("Button that hides this panel. Wired to Hide() in Awake.")]
    [SerializeField] private Button closeButton;

    // ─── PlayerPrefs Keys ─────────────────────────────────────────────────────
    // Shared with SoundManager — must match exactly.
    private const string MusicVolumeKey = "MUSIC_VOLUME";
    private const string SFXVolumeKey = "SFX_VOLUME";
    private const string VibrationKey = "VIBRATION";
    private const string TargetFPSKey = "TARGET_FPS";
    private const int DefaultFPS = 60;

    // ─── Private ──────────────────────────────────────────────────────────────
    /// <summary>
    /// True while LoadSettings() is populating UI controls.
    /// Suppresses the slider/toggle callbacks so saving doesn't fire during init.
    /// </summary>
    private bool _initialising;

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        closeButton?.onClick.AddListener(Hide);

        fps30Button?.onClick.AddListener(() => SetTargetFPS(30));
        fps60Button?.onClick.AddListener(() => SetTargetFPS(60));

        musicSlider?.onValueChanged.AddListener(OnMusicSliderChanged);
        sfxSlider?.onValueChanged.AddListener(OnSFXSliderChanged);

        vibrationToggle?.onValueChanged.AddListener(OnVibrationToggleChanged);
    }

    private void OnEnable()
    {
        // Reload every time the panel becomes visible — handles the case where
        // the player changed a setting from another panel instance (e.g. main menu
        // vs. pause menu) between sessions.
        LoadSettings();
    }

    private void OnDestroy()
    {
        // Remove all listeners to prevent phantom callbacks after destroy.
        closeButton?.onClick.RemoveAllListeners();
        fps30Button?.onClick.RemoveAllListeners();
        fps60Button?.onClick.RemoveAllListeners();
        musicSlider?.onValueChanged.RemoveAllListeners();
        sfxSlider?.onValueChanged.RemoveAllListeners();
        vibrationToggle?.onValueChanged.RemoveAllListeners();
    }

    // ─── Load ─────────────────────────────────────────────────────────────────

    private void LoadSettings()
    {
        _initialising = true;

        float music = PlayerPrefs.GetFloat(MusicVolumeKey, 0.6f);
        if (musicSlider != null) musicSlider.value = music;
        UpdateMusicLabel(music);

        float sfx = PlayerPrefs.GetFloat(SFXVolumeKey, 1f);
        if (sfxSlider != null) sfxSlider.value = sfx;
        UpdateSFXLabel(sfx);

        bool vibration = PlayerPrefs.GetInt(VibrationKey, 1) == 1;
        if (vibrationToggle != null) vibrationToggle.isOn = vibration;

        int fps = PlayerPrefs.GetInt(TargetFPSKey, DefaultFPS);
        ApplyFPS(fps);
        UpdateFPSButtons(fps);

        _initialising = false;
    }

    // ─── Music ────────────────────────────────────────────────────────────────

    private void OnMusicSliderChanged(float value)
    {
        if (_initialising) return;
        SoundManager.Instance?.SetMusicVolume(value);  // SoundManager saves to PlayerPrefs internally.
        UpdateMusicLabel(value);
        SoundManager.Instance?.PlaySFX(SoundType.UIClick);
    }

    private void UpdateMusicLabel(float value)
    {
        if (musicValueLabel != null)
            musicValueLabel.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    // ─── SFX ──────────────────────────────────────────────────────────────────

    private void OnSFXSliderChanged(float value)
    {
        if (_initialising) return;
        SoundManager.Instance?.SetSFXVolume(value);    // SoundManager saves to PlayerPrefs internally.
        UpdateSFXLabel(value);
        SoundManager.Instance?.PlaySFX(SoundType.UIClick);
    }

    private void UpdateSFXLabel(float value)
    {
        if (sfxValueLabel != null)
            sfxValueLabel.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    // ─── Vibration ────────────────────────────────────────────────────────────

    private void OnVibrationToggleChanged(bool isOn)
    {
        if (_initialising) return;
        PlayerPrefs.SetInt(VibrationKey, isOn ? 1 : 0);
        PlayerPrefs.Save();
        SoundManager.Instance?.PlaySFX(SoundType.UIClick);
    }

    /// <summary>
    /// Returns true if the player has vibration enabled.
    /// Call this before triggering any haptic feedback anywhere in the project.
    /// </summary>
    public static bool IsVibrationEnabled() =>
        PlayerPrefs.GetInt(VibrationKey, 1) == 1;

    // ─── Target FPS ───────────────────────────────────────────────────────────

    private void SetTargetFPS(int fps)
    {
        ApplyFPS(fps);
        UpdateFPSButtons(fps);
        PlayerPrefs.SetInt(TargetFPSKey, fps);
        PlayerPrefs.Save();
        SoundManager.Instance?.PlaySFX(SoundType.UIClick);
    }

    private static void ApplyFPS(int fps)
    {
        Application.targetFrameRate = fps;
        QualitySettings.vSyncCount = 0;  // vSync must be off for targetFrameRate to take effect.
    }

    private void UpdateFPSButtons(int activeFPS)
    {
        SetButtonColor(fps30Button, activeFPS == 30 ? activeFPSColor : inactiveFPSColor);
        SetButtonColor(fps60Button, activeFPS == 60 ? activeFPSColor : inactiveFPSColor);
    }

    private static void SetButtonColor(Button btn, Color color)
    {
        if (btn == null) return;
        Image img = btn.GetComponent<Image>();
        if (img != null) img.color = color;
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>Activates the settings panel and plays the UI click sound.</summary>
    public void Show()
    {
        SoundManager.Instance?.PlaySFX(SoundType.UIClick);
        gameObject.SetActive(true);
    }

    /// <summary>Hides the settings panel and plays the UI click sound.</summary>
    public void Hide()
    {
        SoundManager.Instance?.PlaySFX(SoundType.UIClick);
        gameObject.SetActive(false);
    }
}