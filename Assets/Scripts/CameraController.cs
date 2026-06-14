using System.Collections;
using UnityEngine;

/// <summary>
/// Follows the player with a fixed world-space offset and supports
/// short procedural camera shakes for arcade impact feedback.
///
/// Position is set every LateUpdate so it always runs after all
/// character movement has been processed that frame.
///
/// Usage:
///   Camera.main.GetComponent&lt;CameraController&gt;()?.TriggerShake();
///   Camera.main.GetComponent&lt;CameraController&gt;()?.TriggerShake(0.12f, 0.3f);
///
/// Inspector setup:
///   player      — assign the player transform; camera tracks it every frame
///   offset      — world-space offset from the player (behind and above)
///   shakeAmount — default peak displacement per axis during a shake
///   shakeDuration — default shake duration in seconds
/// </summary>
public class CameraController : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Target")]
    [Tooltip("The transform the camera follows. Assign the player root.")]
    public Transform player;

    [Header("Follow Settings")]
    [Tooltip("Fixed world-space offset from the player (usually behind and above the spawn point).")]
    public Vector3 offset = new Vector3(0f, 3f, -8f);

    [Header("Camera Shake")]
    [Tooltip("Default peak displacement per axis during a shake.")]
    public float shakeAmount = 0.15f;
    [Tooltip("Default shake duration in seconds.")]
    public float shakeDuration = 0.1f;

    // ─── Private ──────────────────────────────────────────────────────────────
    /// <summary>Per-frame random offset applied on top of the base follow position.</summary>
    private Vector3 _shakeOffset;
    private Coroutine _shakeRoutine;

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    private void LateUpdate()
    {
        if (player == null) return;

        // Base position: fixed offset from player, no smoothing or deadzone.
        // _shakeOffset is added on top while a shake coroutine is running.
        transform.position = player.position + offset + _shakeOffset;
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Triggers a brief randomised camera shake.
    /// Passing -1 for either parameter uses the Inspector default.
    /// Calling this while a shake is already playing restarts it.
    /// </summary>
    /// <param name="duration">Override shake duration in seconds. -1 = use shakeDuration.</param>
    /// <param name="amount">Override peak displacement. -1 = use shakeAmount.</param>
    public void TriggerShake(float duration = -1f, float amount = -1f)
    {
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(ShakeRoutine(
            duration >= 0f ? duration : shakeDuration,
            amount >= 0f ? amount : shakeAmount
        ));
    }

    // ─── Private Helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Each frame randomises _shakeOffset within a sphere of the given radius.
    /// Depth (Z) is excluded so the camera never drifts toward or away from the scene.
    /// The offset is zeroed when the shake expires so the camera snaps back cleanly.
    /// </summary>
    private IEnumerator ShakeRoutine(float duration, float amount)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            _shakeOffset = Random.insideUnitSphere * amount;
            _shakeOffset.z = 0f;  // Never shake depth
            elapsed += Time.deltaTime;
            yield return null;
        }
        _shakeOffset = Vector3.zero;
    }
}