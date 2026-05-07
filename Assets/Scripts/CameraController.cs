using UnityEngine;
using System.Collections;

public class CameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform player;

    [Header("Follow Settings")]
    public Vector3 offset = new Vector3(0f, 3f, -8f);  // Adjust to taste in Inspector
    public float followSmoothTime = 0.1f;               // Snappiness of player follow
    public float verticalDeadzone = 0.5f;               // Don't move camera for tiny y changes

    [Header("Floor Rise Settings")]
    public float riseSpeed = 4f;                        // Match this to GameManager.riseSpeed

    private Vector3 velocity = Vector3.zero;
    private float floorBaseY = 0f;          // Y position of the current floor
    private float targetFloorY = 0f;        // Y position of the next floor
    private bool isRising = false;

    private void LateUpdate()
    {
        if (player == null) return;

        // Base position tracks the current floor height
        float baseY = Mathf.MoveTowards(floorBaseY, targetFloorY, riseSpeed * Time.deltaTime);
        floorBaseY = baseY;

        // Target position: floor base + offset + player horizontal position
        Vector3 target = new Vector3(
            player.position.x * 0f,    // Lock X — arena is circular, camera stays centred
            baseY + offset.y + GetVerticalOffset(),
            offset.z
        );

        // Smoothly follow
        transform.position = Vector3.SmoothDamp(transform.position, target, ref velocity, followSmoothTime);
    }

    // Only follow player vertically if they move beyond the deadzone (jumps feel responsive,
    // tiny ground bumps don't jitter the camera)
    private float GetVerticalOffset()
    {
        float diff = player.position.y - floorBaseY;
        if (Mathf.Abs(diff) < verticalDeadzone) return 0f;
        return diff - Mathf.Sign(diff) * verticalDeadzone;
    }

    // Call this from GameManager when a floor transition begins
    public void RiseToFloor(float newFloorY)
    {
        targetFloorY = newFloorY;
    }
}