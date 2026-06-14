using UnityEngine;
using TMPro;

/// <summary>
/// Displays a live FPS reading in a TextMeshPro label.
/// Refreshes every updateInterval seconds (default 0.5 s) rather than every
/// frame so the text is readable and the SetText call doesn't occur 60× per second.
///
/// Uses unscaled delta time so it continues working correctly when Time.timeScale
/// is set to 0 (pause screen, hit-stop) or modified values (slow motion).
///
/// Inspector setup:
///   fpsText        — assign the TMP label to display the reading
///   updateInterval — how often the display refreshes (seconds)
/// </summary>
public class FPSCounter : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Tooltip("TextMeshPro label that displays the FPS reading.")]
    [SerializeField] private TextMeshProUGUI fpsText;

    [Tooltip("How often the display updates in seconds. Lower = more reactive, higher = more stable.")]
    [SerializeField] private float updateInterval = 0.5f;

    // ─── Private ──────────────────────────────────────────────────────────────
    private float _timer;
    private int _frameCount;

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    private void Update()
    {
        _timer += Time.unscaledDeltaTime;
        _frameCount++;

        if (_timer < updateInterval) return;

        // Average FPS over the interval rather than snapping to a single frame —
        // prevents the display jumping wildly on frames with GC spikes.
        float fps = _frameCount / _timer;

        if (fpsText != null)
            fpsText.text = Mathf.RoundToInt(fps) + " FPS";

        _timer = 0f;
        _frameCount = 0;
    }
}