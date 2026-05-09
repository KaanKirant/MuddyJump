using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform player;

    [Header("Follow Settings")]
    public Vector3 offset = new Vector3(0f, 3f, -8f);
    public float followSmoothTime = 0.1f;
    public float verticalDeadzone = 0.5f;

    // RiseToFloor is now called every frame from GameManager with the live platform Y,
    // so riseSpeed is no longer needed — we just track the given target directly.
    private float _targetY;
    private Vector3 _velocity;

    private void LateUpdate()
    {
        if (player == null) return;

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
}