using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
public class SwipeDetection : MonoBehaviour
{
    public static SwipeDetection instance;

    public delegate void Swipe(Vector2 direction);
    public event Swipe swipePerformed;

    [SerializeField] private InputAction position, press;
    [SerializeField] private float swipeResistance = 100f;

    private Vector2 initialPos;
    private Vector2 currentPos => position.ReadValue<Vector2>();

    private void Awake()
    {
        instance = this;
        position.Enable();
        press.Enable();
        press.performed += _ => initialPos = currentPos;
        press.canceled += _ => DetectSwipe();
    }

    private void DetectSwipe()
    {
        Vector2 delta = currentPos - initialPos;
        Vector2 direction = Vector2.zero;

        if (Mathf.Abs(delta.x) > swipeResistance)
            direction.x = Mathf.Clamp(delta.x, -1f, 1f);

        // BUG FIX: was incorrectly clamping delta.x instead of delta.y
        if (Mathf.Abs(delta.y) > swipeResistance)
            direction.y = Mathf.Clamp(delta.y, -1f, 1f);

        if (direction != Vector2.zero)
            swipePerformed?.Invoke(direction);
    }
}