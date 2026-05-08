using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform player;

    [Header("Follow Settings")]
    public Vector3 offset = new Vector3(0f, 3f, -8f);
    public float followSmoothTime = 0.1f;
    public float verticalDeadzone = 0.5f;

    [Header("Floor Rise")]
    public float riseSpeed = 4f;    // Should match GameManager.riseSpeed

    private Vector3 _velocity;
    private float _floorBaseY;    // Current interpolated floor height
    private float _targetFloorY;  // Next floor Y we're rising toward

    private void LateUpdate()
    {
        if (player == null) return;

        _floorBaseY = Mathf.MoveTowards(_floorBaseY, _targetFloorY, riseSpeed * Time.deltaTime);

        Vector3 target = new Vector3(
            0f,                                             // Arena is circular — lock X
            _floorBaseY + offset.y + GetVerticalOffset(),
            offset.z
        );

        transform.position = Vector3.SmoothDamp(transform.position, target, ref _velocity, followSmoothTime);
    }

    // Follows the player vertically only beyond the deadzone,
    // so small ground bumps don't jitter the camera.
    private float GetVerticalOffset()
    {
        float diff = player.position.y - _floorBaseY;
        if (Mathf.Abs(diff) < verticalDeadzone) return 0f;
        return diff - Mathf.Sign(diff) * verticalDeadzone;
    }

    // Called by GameManager before the rise starts
    public void RiseToFloor(float newFloorY) => _targetFloorY = newFloorY;
}