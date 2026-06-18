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
/// Keyboard support (for WebGL/desktop builds, e.g. itch.io):
///   Arrow keys and WASD both raise the same SwipePerformed event with a unit
///   direction vector, so every downstream listener (PlayerMovement, tutorial,
///   etc.) needs zero changes — keyboard input is indistinguishable from a swipe.
///   W/Up = jump, S/Down = fast fall, D/Right = kick right, A/Left = kick left.
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
    /// Fired when a valid swipe — or equivalent keyboard press — is detected.
    /// Argument is the normalised direction. Subscribe in OnEnable, unsubscribe in OnDisable.
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

    [Header("Keyboard Settings")]
    [Tooltip("Enable arrow key / WASD input as an alternative to swipes. Intended for WebGL/desktop builds.")]
    [SerializeField] private bool enableKeyboardInput = true;

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

    private void Update()
    {
        if (enableKeyboardInput) PollKeyboard();
    }

    // ─── Keyboard Input ───────────────────────────────────────────────────────

    /// <summary>
    /// Polls arrow keys and WASD via the legacy Input class (works alongside the
    /// new Input System without requiring a separate InputAction asset entry).
    /// Fires the same SwipePerformed event a touch swipe would, on key-down only.
    /// </summary>
    private void PollKeyboard()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame)
            SwipePerformed?.Invoke(Vector2.up);
        else if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame)
            SwipePerformed?.Invoke(Vector2.down);
        else if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame)
            SwipePerformed?.Invoke(Vector2.right);
        else if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame)
            SwipePerformed?.Invoke(Vector2.left);
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