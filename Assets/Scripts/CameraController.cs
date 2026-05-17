using UnityEngine;
using System.Collections;

public class CameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform player;

    [Header("Follow Settings")]
    [Tooltip("Fixed offset from the arena origin (usually behind and above the player spawn).")]
    public Vector3 offset = new Vector3(0f, 3f, -8f);

    [Header("Camera Shake")]
    [Tooltip("Maximum displacement per axis during shake.")]
    public float shakeAmount = 0.15f;
    [Tooltip("Default shake duration in seconds.")]
    public float shakeDuration = 0.1f;


    private Vector3 _shakeOffset;
    private Coroutine _shakeRoutine;

    private void LateUpdate()
    {
        if (player == null) return;

        // Static offset — no smoothing, no follow, no deadzone
        // Camera position is just player.position + offset (in world space relative to arena)
        transform.position = player.position + offset;
    }

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