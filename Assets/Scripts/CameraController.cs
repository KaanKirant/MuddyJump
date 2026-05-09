using System.Collections;
using UnityEngine;

/// <summary>
/// Follows the rising platform every frame and smoothly tracks the player's
/// vertical position beyond a deadzone. Supports a trauma-based screen shake
/// that can be triggered externally (e.g. fast-fall landing, pipe hit).
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform player;

    [Header("Follow Settings")]
    public Vector3 offset = new Vector3(0f, 3f, -8f);
    public float followSmoothTime = 0.1f;
    [Tooltip("Player must move this many units above the platform before camera follows vertically.")]
    public float verticalDeadzone = 0.5f;

    [Header("Screen Shake")]
    [Tooltip("Default shake magnitude used by TriggerShake().")]
    public float defaultShakeMagnitude = 0.15f;
    [Tooltip("Default shake duration in seconds.")]
    public float defaultShakeDuration = 0.12f;

    // Set every frame by GameManager.RisePlatform() — tracks live platform Y
    private float _targetY;
    private Vector3 _smoothDampVelocity;

    // Shake state
    private float _shakeMagnitude;
    private float _shakeTimer;

    #region Unity Lifecycle

    private void Start()
    {
        // Subscribe to player fast-fall landing for automatic shake
        // FindAnyObjectByType is fine here — called once at Start
        PlayerMovement pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null)
            pm.OnFastFallLanded += OnFastFallLanded;
    }

    private void OnDestroy()
    {
        PlayerMovement pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null)
            pm.OnFastFallLanded -= OnFastFallLanded;
    }

    private void LateUpdate()
    {
        if (player == null) return;

        // Base target: platform Y + offset + deadzone-gated player vertical offset
        Vector3 target = new Vector3(
            0f,                                              // Arena is circular — X is always locked
            _targetY + offset.y + GetVerticalOffset(),
            offset.z
        );

        transform.position = Vector3.SmoothDamp(
            transform.position, target, ref _smoothDampVelocity, followSmoothTime
        );

        // Shake is added on top of the smooth position using unscaled time
        // so it survives landing freeze frames (Time.timeScale near zero)
        if (_shakeTimer > 0f)
        {
            _shakeTimer -= Time.unscaledDeltaTime;
            float t = Mathf.Max(_shakeTimer, 0f);                         // Never go negative
            Vector3 shake = Random.insideUnitSphere * (_shakeMagnitude * t);
            shake.z = 0f;                                                  // Never push into/out of scene
            transform.position += shake;
        }
    }

    #endregion

    #region Vertical Follow

    /// <summary>
    /// Returns a vertical offset only when the player is beyond the deadzone.
    /// Keeps the camera stable during ground bumps while still tracking jumps.
    /// </summary>
    private float GetVerticalOffset()
    {
        float diff = player.position.y - _targetY;
        if (Mathf.Abs(diff) < verticalDeadzone) return 0f;
        return diff - Mathf.Sign(diff) * verticalDeadzone;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Called by GameManager every frame with the live platform world Y.
    /// No interpolation needed here — SmoothDamp handles smoothing.
    /// </summary>
    public void RiseToFloor(float newTargetY) => _targetY = newTargetY;

    /// <summary>
    /// Triggers a screen shake. Takes the stronger value if one is already running.
    /// </summary>
    public void TriggerShake(float magnitude, float duration)
    {
        // Don't interrupt a stronger shake with a weaker one
        if (magnitude >= _shakeMagnitude || _shakeTimer <= 0f)
        {
            _shakeMagnitude = magnitude;
            _shakeTimer = duration;
        }
    }

    /// <summary>Shake with Inspector defaults — call this from game events.</summary>
    public void TriggerShake() => TriggerShake(defaultShakeMagnitude, defaultShakeDuration);

    #endregion

    #region Event Handlers

    private void OnFastFallLanded() => TriggerShake();

    #endregion
}