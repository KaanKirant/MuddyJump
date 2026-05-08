using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
public class SwipeDetection : MonoBehaviour
{
    public static SwipeDetection Instance { get; private set; }

    public event Action<Vector2> SwipePerformed;

    [Header("Input")]
    [SerializeField] private InputAction position;
    [SerializeField] private InputAction press;

    [Header("Swipe Settings")]
    [SerializeField] private float swipeResistance = 100f;

    private Vector2 _initialPos;
    private bool _isPressed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
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
        press.performed -= OnPressStarted;
        press.canceled -= OnPressReleased;

        position.Disable();
        press.Disable();
    }

    private void OnPressStarted(InputAction.CallbackContext _)
    {
        _initialPos = position.ReadValue<Vector2>();
        _isPressed = true;
    }

    private void OnPressReleased(InputAction.CallbackContext _)
    {
        if (!_isPressed) return;
        _isPressed = false;
        DetectSwipe();
    }

    private void DetectSwipe()
    {
        Vector2 delta = position.ReadValue<Vector2>() - _initialPos;
        if (delta.sqrMagnitude < swipeResistance * swipeResistance) return;

        SwipePerformed?.Invoke(delta.normalized);
    }
}