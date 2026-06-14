using UnityEngine;

/// <summary>
/// Controls visibility of the main menu panel.
///
/// Show() and Hide() are wired to buttons in the Inspector.
/// Both play a UI click sound so every transition gives audio feedback.
///
/// This panel is intentionally minimal — it delegates all settings to
/// SettingsPanel and all navigation to scene-loading calls on GameManager.
/// </summary>
public class MainMenuPanel : MonoBehaviour
{
    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>Activates this panel and plays the UI click sound.</summary>
    public void Show()
    {
        SoundManager.Instance?.PlaySFX(SoundType.UIClick);
        gameObject.SetActive(true);
    }

    /// <summary>Deactivates this panel and plays the UI click sound.</summary>
    public void Hide()
    {
        SoundManager.Instance?.PlaySFX(SoundType.UIClick);
        gameObject.SetActive(false);
    }
}