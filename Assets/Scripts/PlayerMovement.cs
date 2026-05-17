using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Handles all player input, physics, kick window, health, and damage.
///
/// Health is stored in PlayerStats (singleton) so other systems can read it.
/// PlayerMovement drives regen and damage; PlayerStats owns the values.
///
/// Input → SwipeDetection.SwipePerformed → OnSwipe()
///   Swipe up         → TryJump
///   Swipe down       → TryFastFall
///   Swipe left/right → TryKick
///
/// Kick window opens on swipe, FixedUpdate polls CheckKickContact() every
/// physics tick — far more reliable than a single animation event frame.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{
    // ─── Health / Regen ───────────────────────────────────────────────────────
    [Header("Health")]
    [Tooltip("Seconds to regenerate one full heart.")]
    [SerializeField] private float lifeRegenInterval = 15f;
    [Tooltip("Grace window after a hit — prevents chain damage.")]
    [SerializeField] private float hitInvincibilityDuration = 2f;

    // ─── Physics ──────────────────────────────────────────────────────────────
    [Header("Physics Settings")]
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private float slamForce = 18f;
    [SerializeField] private float fallGravity = 30f;

    // ─── Actions ──────────────────────────────────────────────────────────────
    [Header("Action Settings")]
    [SerializeField] private float actionCooldown = 0.2f;

    // ─── Kick ─────────────────────────────────────────────────────────────────
    [Header("Kick Settings")]
    [SerializeField] private float kickRange = 1.5f;
    [SerializeField] private float kickHeightOffset = 0.5f;
    [SerializeField] private LayerMask pipeLayer;
    [Tooltip("How long the hit window stays open after swipe.")]
    [SerializeField] private float kickWindowDuration = 0.2f;
    [SerializeField] private float kickInvincibilityDuration = 0.4f;

    // ─── Debug ────────────────────────────────────────────────────────────────
    [Header("Debug")]
    [SerializeField] private bool isGrounded;
    [SerializeField] private bool _kickWindowOpen;

    // ─── Public State ─────────────────────────────────────────────────────────
    public bool IsKicking { get; private set; }
    public bool IsInvincible { get; private set; }

    // ─── Private ──────────────────────────────────────────────────────────────
    private Rigidbody _rb;
    private Animator _animator;
    private PipeLogic _pipe;

    private Vector2 _currentKickDirection;
    private float _lastJumpTime;
    private float _lastKickTime;
    private bool _kickLandedThisSwing;

    private Coroutine _invincibilityRoutine;
    private Coroutine _kickWindowRoutine;
    private Coroutine _regenRoutine;

    private readonly Collider[] _kickHits = new Collider[4];

    private static readonly int IsGroundHash = Animator.StringToHash("isGround");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int RollHash = Animator.StringToHash("Roll");
    private static readonly int KickRightHash = Animator.StringToHash("kickRight");
    private static readonly int KickLeftHash = Animator.StringToHash("kickLeft");
    private static readonly int IdleHash = Animator.StringToHash("Idle");

    #region Unity Lifecycle

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();

        _rb.constraints = RigidbodyConstraints.FreezeRotation
                        | RigidbodyConstraints.FreezePositionX
                        | RigidbodyConstraints.FreezePositionZ;

        _pipe = FindAnyObjectByType<PipeLogic>();
    }

    private void OnEnable()
    {
        if (SwipeDetection.Instance != null)
            SwipeDetection.Instance.SwipePerformed += OnSwipe;
    }

    private void OnDisable()
    {
        if (SwipeDetection.Instance != null)
            SwipeDetection.Instance.SwipePerformed -= OnSwipe;
    }

    private void Start()
    {
        _regenRoutine = StartCoroutine(RegenLoop());
    }

    private void FixedUpdate()
    {
        if (!isGrounded)
            _rb.AddForce(Vector3.down * fallGravity, ForceMode.Acceleration);

        if (_kickWindowOpen)
            CheckKickContact();
    }

    #endregion

    #region Input

    private void OnSwipe(Vector2 direction)
    {
        if (direction.y > 0.5f) TryJump();
        else if (direction.y < -0.5f) TryFastFall();
        else if (Mathf.Abs(direction.x) > 0.5f) TryKick(direction);
    }

    #endregion

    #region Jump

    private void TryJump()
    {
        if (!isGrounded || Time.time < _lastJumpTime + actionCooldown) return;
        DoJump();
    }

    private void DoJump()
    {
        _lastJumpTime = Time.time;

        // Platform no longer moves — just zero Y velocity and add impulse
        Vector3 v = _rb.linearVelocity;
        v.y = 0f;
        _rb.linearVelocity = v;

        _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        _animator.CrossFade(JumpHash, 0.05f);
    }

    #endregion

    #region Fast Fall

    private void TryFastFall()
    {
        if (isGrounded) return;
        DoFastFall();
    }

    private void DoFastFall()
    {
        // Platform no longer moves — just set Y velocity directly
        Vector3 v = _rb.linearVelocity;
        v.y = -slamForce;
        _rb.linearVelocity = v;
        _rb.angularVelocity = Vector3.zero;

        _animator.CrossFade(RollHash, 0.05f);

        // Instant visual feedback: camera shake on slam initiation for arcade feel
        CameraController camera = Camera.main?.GetComponent<CameraController>();
        if (camera != null) camera.TriggerShake(0.08f, 0.12f);

        // Light hit-stop to emphasize the slam commitment
        if (GameManager.instance != null) GameManager.instance.TriggerHitStop(0.2f, 0.02f);
    }

    #endregion

    #region Kick

    private void TryKick(Vector2 direction)
    {
        if (!isGrounded || Time.time < _lastKickTime + actionCooldown) return;
        DoKick(direction);
    }

    private void DoKick(Vector2 direction)
    {
        _lastKickTime = Time.time;
        _currentKickDirection = direction;
        _kickLandedThisSwing = false;

        _animator.CrossFade(direction.x > 0f ? KickRightHash : KickLeftHash, 0.02f);

        // Window opens on animation event — aligns hit detection with actual wind-up
    }

    /// <summary>Animation event — fires at wind-up completion (foot starts moving).</summary>
    public void OnKickWindowOpen()
    {
        _kickLandedThisSwing = false;

        if (_kickWindowRoutine != null) StopCoroutine(_kickWindowRoutine);
        _kickWindowRoutine = StartCoroutine(KickWindowRoutine());
    }

    /// <summary>Animation event — optional early close at follow-through end.</summary>
    public void OnKickWindowClose() => CloseKickWindow();

    /// <summary>Legacy event name — safe fallback if old clips have this event.</summary>
    public void OnKickImpact() => CheckKickContact();

    private IEnumerator KickWindowRoutine()
    {
        IsKicking = true;
        _kickWindowOpen = true;

        yield return new WaitForSeconds(kickWindowDuration);

        CloseKickWindow();
    }

    private void CloseKickWindow()
    {
        _kickWindowOpen = false;
        IsKicking = false;

        if (_kickWindowRoutine != null) { StopCoroutine(_kickWindowRoutine); _kickWindowRoutine = null; }
    }

    /// <summary>
    /// Polled every FixedUpdate tick while window is open.
    /// Multi-frame window makes timing forgiving while still requiring correct direction.
    /// </summary>
    private void CheckKickContact()
    {
        if (_kickLandedThisSwing || _pipe == null) return;

        Vector3 origin = transform.position - new Vector3(0f, kickHeightOffset, 0f);
        int hitCount = Physics.OverlapSphereNonAlloc(origin, kickRange, _kickHits, pipeLayer);
        if (hitCount == 0) return;

        bool validDirection = (_currentKickDirection.x > 0f && _pipe.rotationDirection) ||
                              (_currentKickDirection.x < 0f && !_pipe.rotationDirection);
        if (!validDirection) return;

        bool landed = _pipe.GetKicked(_currentKickDirection);
        if (!landed) return;

        _kickLandedThisSwing = true;
        CloseKickWindow();

        GameManager.instance.AddBonusScore(1);

        // Lighter hit-stop on successful kick for responsive feedback (timescale 0.15, 0.03s)
        // This feels crisp without disrupting gameplay flow
        if (GameManager.instance != null) GameManager.instance.TriggerHitStop(0.15f, 0.03f);

        // Camera shake on successful kick impact
        CameraController camera = Camera.main?.GetComponent<CameraController>();
        if (camera != null) camera.TriggerShake(0.06f, 0.15f);

        if (_invincibilityRoutine != null) StopCoroutine(_invincibilityRoutine);
        _invincibilityRoutine = StartCoroutine(KickInvincibility());
    }

    #endregion

    #region Health & Damage

    public void TakeDamage(int amount)
    {
        if (IsInvincible) return;

        // Kill lateral drift on hit
        Vector3 v = _rb.linearVelocity; v.x = 0f; v.z = 0f; _rb.linearVelocity = v;
        _rb.angularVelocity = Vector3.zero;

        PlayerStats.Instance.TakeDamage(amount);

        if (PlayerStats.Instance.Health < 1f)
        {
            GameManager.instance.EndGame();
            return;
        }

        if (_invincibilityRoutine != null) StopCoroutine(_invincibilityRoutine);
        _invincibilityRoutine = StartCoroutine(HitInvincibility());

        // Reset regen — must survive a full interval before recovering
        RestartRegenLoop();
    }

    /// <summary>Instant death — bypasses normal invincibility (used by lethal pipes).</summary>
    public void InstantKill()
    {
        GameManager.instance.EndGame();
    }

    /// <summary>
    /// Fills one heart at a time over lifeRegenInterval seconds.
    /// Restarted from zero by TakeDamage — damage resets the regen timer.
    /// </summary>
    private IEnumerator RegenLoop()
    {
        while (true)
        {
            // Sleep until health is missing
            if (PlayerStats.Instance.Health >= PlayerStats.Instance.MaxHealth)
                yield return new WaitUntil(() => PlayerStats.Instance.Health < PlayerStats.Instance.MaxHealth);

            // Fill toward the next whole heart
            float target = Mathf.Min(Mathf.Floor(PlayerStats.Instance.Health) + 1f, PlayerStats.Instance.MaxHealth);

            while (PlayerStats.Instance.Health < target)
            {
                PlayerStats.Instance.Heal(Time.deltaTime / lifeRegenInterval);
                yield return null;
            }
        }
    }

    private void RestartRegenLoop()
    {
        if (_regenRoutine != null) StopCoroutine(_regenRoutine);
        _regenRoutine = StartCoroutine(RegenLoop());
    }

    #endregion

    #region Coroutines

    private IEnumerator KickInvincibility()
    {
        IsInvincible = true;
        yield return new WaitForSeconds(kickInvincibilityDuration);
        IsInvincible = false;
    }

    private IEnumerator HitInvincibility()
    {
        IsInvincible = true;
        yield return new WaitForSeconds(hitInvincibilityDuration);
        IsInvincible = false;
    }

    #endregion

    #region Ground Detection

    private void OnCollisionStay(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Ground")) return;
        if (!isGrounded) _animator.CrossFade(IdleHash, 0.05f);
        isGrounded = true;
        _animator.SetBool(IsGroundHash, true);
    }

    private void OnCollisionExit(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Ground")) return;
        isGrounded = false;
        _animator.SetBool(IsGroundHash, false);
    }

    #endregion

    #region Public API

    public void SetPipeLogic(PipeLogic targetPipe) => _pipe = targetPipe;
    public void SetKickState(bool value) => IsKicking = value;

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _kickWindowOpen ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(transform.position - new Vector3(0f, kickHeightOffset, 0f), kickRange);
    }

    #endregion
}