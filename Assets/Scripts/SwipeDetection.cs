using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Detects touch/mouse swipes and fires SwipePerformed with a normalised 2D direction.
///
/// Uses Unity's new Input System with two serialized InputActions:
///   position — reads the current pointer/touch position (Vector2)
///   press    — fires performed on press-down and canceled on release
///
/// A swipe is only registered when the pointer travels at least swipeResistance
/// pixels between press-down and release. This threshold prevents taps from being
/// misread as micro-swipes.
///
/// [DefaultExecutionOrder(-100)] ensures this runs before any subscriber
/// (PlayerMovement, etc.) so no swipe event is ever missed on the first frame.
///
/// Inspector setup:
///   Assign the position and press InputActions from your PlayerInputActions asset.
///   swipeResistance — tune for target device screen density (100 px is typical).
/// </summary>
[DefaultExecutionOrder(-100)]
public class SwipeDetection : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static SwipeDetection Instance { get; private set; }

    // ─── Events ───────────────────────────────────────────────────────────────
    /// <summary>
    /// Fired when a valid swipe is detected. Argument is the normalised swipe direction.
    /// Subscribe in OnEnable, unsubscribe in OnDisable.
    /// </summary>
    public event Action<Vector2> SwipePerformed;

    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Input Actions")]
    [Tooltip("Pointer position action (Touchscreen primaryTouch/position or Mouse/position).")]
    [SerializeField] private InputAction position;

    [Tooltip("Pointer press action (Touchscreen primaryTouch/press or Mouse/leftButton).")]
    [SerializeField] private InputAction press;

    [Header("Swipe Settings")]
    [Tooltip("Minimum pixel distance the finger must travel to register as a swipe.")]
    [SerializeField] private float swipeResistance = 100f;

    // ─── Private ──────────────────────────────────────────────────────────────
    private Vector2 _initialPos;
    private bool _isPressed;

    /// <summary>
    /// Pre-squared threshold — avoids recomputing every release.
    /// Cached in Awake because swipeResistance is an Inspector field (never changes at runtime).
    /// </summary>
    private float _swipeResistanceSqr;

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _swipeResistanceSqr = swipeResistance * swipeResistance;
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
        // Always unsubscribe before disabling — prevents phantom callbacks after scene unload.
        press.performed -= OnPressStarted;
        press.canceled -= OnPressReleased;

        position.Disable();
        press.Disable();
    }

    // ─── Input Handlers ───────────────────────────────────────────────────────

    private void OnPressStarted(InputAction.CallbackContext _)
    {
        _initialPos = position.ReadValue<Vector2>();
        _isPressed = true;
    }

    private void OnPressReleased(InputAction.CallbackContext _)
    {
        // Guard: canceled can fire without a prior performed on scene reload.
        if (!_isPressed) return;
        _isPressed = false;
        DetectSwipe();
    }

    /// <summary>
    /// Compares release position against press position.
    /// Fires SwipePerformed only when the travel distance clears the threshold.
    /// Uses sqrMagnitude to avoid a sqrt on every release.
    /// </summary>
    private void DetectSwipe()
    {
        Vector2 delta = position.ReadValue<Vector2>() - _initialPos;

        if (delta.sqrMagnitude < _swipeResistanceSqr) return;

        SwipePerformed?.Invoke(delta.normalized);
    }
}