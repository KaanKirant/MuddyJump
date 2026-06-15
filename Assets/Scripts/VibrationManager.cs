using UnityEngine;

/// <summary>
/// Platform-safe wrapper around Unity's Handheld.Vibrate().
///
/// Handheld.Vibrate() is a single fixed-length buzz — no duration or pattern
/// control is available without a third-party plugin. This class wraps it with:
///   - A player preference check (respects the vibration toggle in SettingsPanel)
///   - Platform guards so the call never reaches non-mobile builds
///   - Named trigger methods so call sites are self-documenting
///
/// Usage from anywhere:
///   VibrationManager.PipeHit();
///   VibrationManager.ShieldBreak();
///
/// No scene setup required — all methods are static.
/// The vibration toggle is read from PlayerPrefs via SettingsPanel.IsVibrationEnabled()
/// on every call so changes take effect immediately without a restart.
/// </summary>
public static class VibrationManager
{
    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Short buzz for a pipe hit. Called from PipeLogic when the pipe strikes the player.
    /// </summary>
    public static void PipeHit() => Vibrate();

    /// <summary>
    /// Short buzz when the player's shield absorbs a hit and breaks.
    /// Called from PlayerMovement.BreakShield() via TakeDamage / InstantKill.
    /// </summary>
    public static void ShieldBreak() => Vibrate();

    // ─── Core ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Triggers a vibration if the player has it enabled and the platform supports it.
    /// Silent no-op in the Editor and on unsupported platforms (PC, console).
    /// </summary>
    private static void Vibrate()
    {
        if (!SettingsPanel.IsVibrationEnabled()) return;

#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }
}