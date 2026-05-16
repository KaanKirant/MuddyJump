using UnityEngine;
using System.Collections;

public class CameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform player;

    [Header("Follow Settings")]
    public Vector3 offset = new Vector3(0f, 3f, -8f);
    public float followSmoothTime = 0.1f;
    public float verticalDeadzone = 0.5f;


    [Header("Camera Shake")]
    [Tooltip("Maximum displacement per axis during shake.")]
    public float shakeAmount = 0.15f;
    [Tooltip("Default shake duration in seconds.")]
    public float shakeDuration = 0.1f;

    // RiseToFloor is now called every frame from GameManager with the live platform Y,
    // so riseSpeed is no longer needed — we just track the given target directly.
    private float _targetY;
    private Vector3 _velocity;
    private Vector3 _shakeOffset;
    private Coroutine _shakeRoutine;

    private void LateUpdate()
    {
        if (player == null) return;

        // Calculate the target Y: follow the player only within the deadzone margin
        // to avoid camera jitter from minor platform bumps or physics micromovements.
        float diff = player.position.y - _targetY;
        float verticalOffset = Mathf.Abs(diff) < verticalDeadzone ? 0f : diff - Mathf.Sign(diff) * verticalDeadzone;

        Vector3 target = new Vector3(
            0f,                                              // Arena is circular — lock X
            _targetY + offset.y + GetVerticalOffset(),
            offset.z
        );

        transform.position = Vector3.SmoothDamp(
            transform.position, target, ref _velocity, followSmoothTime
        );
    }

    // Follows the player vertically only when they move beyond the deadzone,
    // so minor ground bumps don't jitter the camera.
    private float GetVerticalOffset()
    {
        float diff = player.position.y - _targetY;
        if (Mathf.Abs(diff) < verticalDeadzone) return 0f;
        return diff - Mathf.Sign(diff) * verticalDeadzone;
    }

    // GameManager calls this every frame (or on floor transitions).
    // Accepts the live platform world Y directly.
    public void RiseToFloor(float newTargetY) => _targetY = newTargetY;

    /// <summary>
    /// Triggers a brief camera shake for impact feedback.
    /// Call this on hit/kick impacts for arcade game feel.
    /// </summary>
    public void TriggerShake(float duration = -1f, float amount = -1f)
    {
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(ShakeRoutine(
            duration >= 0 ? duration : shakeDuration,
            amount >= 0 ? amount : shakeAmount
        ));
    }

    private IEnumerator ShakeRoutine(float duration, float amount)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            _shakeOffset = Random.insideUnitSphere * amount;
            _shakeOffset.z = 0f;  // Don't shake depth
            elapsed += Time.deltaTime;
            yield return null;
        }
        _shakeOffset = Vector3.zero;
    }
}