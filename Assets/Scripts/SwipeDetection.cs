using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Detects touch/mouse swipes and fires SwipePerformed with a normalised direction.
/// Execution order -100 ensures this runs before any subscriber (PlayerMovement etc.)
/// so input is never missed on the first frame.
/// </summary>
[DefaultExecutionOrder(-100)]
public class SwipeDetection : MonoBehaviour
{
    public static SwipeDetection Instance { get; private set; }

    /// <summary>Normalised swipe direction. Subscribe in OnEnable, unsubscribe in OnDisable.</summary>
    public event Action<Vector2> SwipePerformed;

    [Header("Input Actions")]
    [SerializeField] private InputAction position;  // Pointer position (Touchscreen/Mouse)
    [SerializeField] private InputAction press;     // Pointer press (started = down, canceled = up)

    [Header("Swipe Settings")]
    [Tooltip("Minimum pixel distance the finger must travel to register as a swipe.")]
    [SerializeField] private float swipeResistance = 100f;

    private Vector2 _initialPos;
    private bool _isPressed;

    // Cached sqr threshold — avoids recomputing every release
    private float SwipeResistanceSqr => swipeResistance * swipeResistance;

    #region Unity Lifecycle

    private void Awake()
    {
        // Singleton — destroy duplicate if reloaded
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        position.Enable();
        press.Enable();

        press.performed += OnPressStarted;
        press.canceled += OnPressReleased;
    }

    private void OnDisable()
    {
        // Always unsubscribe — prevents ghost callbacks after scene unload
        press.performed -= OnPressStarted;
        press.canceled -= OnPressReleased;

        position.Disable();
        press.Disable();
    }

    #endregion

    #region Input Handlers

    private void OnPressStarted(InputAction.CallbackContext _)
    {
        _initialPos = position.ReadValue<Vector2>();
        _isPressed = true;
    }

    private void OnPressReleased(InputAction.CallbackContext _)
    {
        // Guard: canceled can fire without a prior performed on scene reload
        if (!_isPressed) return;
        _isPressed = false;
        DetectSwipe();
    }

    private void DetectSwipe()
    {
        Vector2 delta = position.ReadValue<Vector2>() - _initialPos;

        // sqrMagnitude avoids a sqrt — cheaper than magnitude on mobile
        if (delta.sqrMagnitude < SwipeResistanceSqr) return;

        SwipePerformed?.Invoke(delta.normalized);
    }

    #endregion
}