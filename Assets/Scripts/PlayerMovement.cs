using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{
    // ─── Lives ────────────────────────────────────────────────────────────────
    [Header("Lives")]
    [SerializeField] private int maxLives = 3;
    [SerializeField] private float lifeRegenInterval = 15f;
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
    [SerializeField] private float kickWindowDuration = 0.2f;   // How long the hit window stays open
    [SerializeField] private float kickInvincibilityDuration = 0.4f;

    // ─── Debug ────────────────────────────────────────────────────────────────
    [Header("Debug")]
    [SerializeField] private bool isGrounded;
    [SerializeField] private bool _kickWindowOpen;  // Visible in Inspector for tuning

    // ─── Public State ─────────────────────────────────────────────────────────
    public int CurrentLives { get; private set; }
    public bool IsKicking { get; private set; }
    public bool IsInvincible { get; private set; }

    // UI subscribes: int = current lives, float = regen progress 0→1
    public event Action<int, float> OnLivesChanged;

    // ─── Private ──────────────────────────────────────────────────────────────
    private Rigidbody _rb;
    private Animator _animator;
    private PipeLogic _pipe;

    private Vector2 _currentKickDirection;
    private float _lastJumpTime;
    private float _lastKickTime;
    private bool _kickLandedThisSwing;   // Prevents scoring multiple hits per swing

    private Coroutine _invincibilityRoutine;
    private Coroutine _kickWindowRoutine;
    private Coroutine _regenRoutine;

    private readonly Collider[] _kickHits = new Collider[4];

    // Animator hashes
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
        CurrentLives = maxLives;
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
        _regenRoutine = StartCoroutine(LivesRegenLoop());
    }

    private void FixedUpdate()
    {
        if (!isGrounded)
            _rb.AddForce(Vector3.down * fallGravity, ForceMode.Acceleration);

        // Poll the kick window every physics tick instead of relying on a single
        // animation event frame — gives ~4-6 frames of valid contact window on mobile
        if (_kickWindowOpen)
            CheckKickContact();
    }

    #endregion

    #region Input

    private void OnSwipe(Vector2 direction)
    {
        if (direction.y > 0.5f) { TryJump(); return; }
        if (direction.y < -0.5f) { TryFastFall(); return; }
        if (Mathf.Abs(direction.x) > 0.5f) TryKick(direction);
    }

    #endregion

    #region Actions

    private void TryJump()
    {
        if (!isGrounded || Time.time < _lastJumpTime + actionCooldown) return;
        DoJump();
    }

    private void DoJump()
    {
        _lastJumpTime = Time.time;

        float platformVY = GameManager.instance != null ? GameManager.instance.CurrentRiseSpeed : 0f;
        Vector3 v = _rb.linearVelocity;
        v.y = platformVY;
        _rb.linearVelocity = v;

        _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        _animator.CrossFade(JumpHash, 0.05f);
    }

    private void TryFastFall()
    {
        if (isGrounded) return;
        DoFastFall();
    }

    private void DoFastFall()
    {
        float platformVY = GameManager.instance != null ? GameManager.instance.CurrentRiseSpeed : 0f;
        Vector3 v = _rb.linearVelocity;
        v.y = platformVY - slamForce;
        _rb.linearVelocity = v;

        _rb.angularVelocity = Vector3.zero;
        _animator.CrossFade(RollHash, 0.05f);
    }

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

        _animator.CrossFade(direction.x > 0f ? KickRightHash : KickLeftHash, 0.03f);
        // Kick window is opened by animation event OnKickWindowOpen,
        // not here — so it aligns with the actual animation wind-up
    }

    #endregion

    #region Kick Window

    // ── Called by animation event at wind-up completion (foot starts moving forward)
    public void OnKickWindowOpen()
    {
        _kickLandedThisSwing = false;

        if (_kickWindowRoutine != null) StopCoroutine(_kickWindowRoutine);
        _kickWindowRoutine = StartCoroutine(KickWindowRoutine());
    }

    // ── Called by animation event at follow-through end (optional — window also auto-closes)
    public void OnKickWindowClose()
    {
        CloseKickWindow();
    }

    // Kept for backwards compatibility if old event name is on clips
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

        if (_kickWindowRoutine != null)
        {
            StopCoroutine(_kickWindowRoutine);
            _kickWindowRoutine = null;
        }
    }

    private void CheckKickContact()
    {
        // Only score one hit per swing — window stays open for feel but won't double-count
        if (_kickLandedThisSwing || _pipe == null) return;

        Vector3 origin = transform.position - new Vector3(0f, kickHeightOffset, 0f);
        int hitCount = Physics.OverlapSphereNonAlloc(origin, kickRange, _kickHits, pipeLayer);
        if (hitCount == 0) return;

        bool validDirection = (_currentKickDirection.x > 0f && _pipe.rotationDirection) ||
                              (_currentKickDirection.x < 0f && !_pipe.rotationDirection);
        if (!validDirection) return;

        bool landed = _pipe.GetKicked(_currentKickDirection);
        if (!landed) return;

        _kickLandedThisSwing = true;   // Lock out further hits this swing
        CloseKickWindow();              // Close immediately on success — no need to stay open

        GameManager.instance.AddBonusScore(1);

        if (_invincibilityRoutine != null) StopCoroutine(_invincibilityRoutine);
        _invincibilityRoutine = StartCoroutine(KickInvincibility());
    }

    #endregion

    #region Lives & Damage

    public void TakeDamage(int amount)
    {
        if (IsInvincible) return;

        Vector3 v = _rb.linearVelocity; v.x = 0f; v.z = 0f; _rb.linearVelocity = v;
        _rb.angularVelocity = Vector3.zero;

        CurrentLives -= amount;
        NotifyLivesChanged();

        if (CurrentLives <= 0)
        {
            CurrentLives = 0;
            GameManager.instance.EndGame();
            return;
        }

        if (_invincibilityRoutine != null) StopCoroutine(_invincibilityRoutine);
        _invincibilityRoutine = StartCoroutine(HitInvincibility());

        RestartRegenLoop();
    }

    private IEnumerator LivesRegenLoop()
    {
        while (true)
        {
            if (CurrentLives >= maxLives)
                yield return new WaitUntil(() => CurrentLives < maxLives);

            float elapsed = 0f;
            while (elapsed < lifeRegenInterval)
            {
                elapsed += Time.deltaTime;
                OnLivesChanged?.Invoke(CurrentLives, elapsed / lifeRegenInterval);
                yield return null;
            }

            CurrentLives = Mathf.Min(CurrentLives + 1, maxLives);
            NotifyLivesChanged();
        }
    }

    private void RestartRegenLoop()
    {
        if (_regenRoutine != null) StopCoroutine(_regenRoutine);
        _regenRoutine = StartCoroutine(LivesRegenLoop());
    }

    private void NotifyLivesChanged() => OnLivesChanged?.Invoke(CurrentLives, 0f);

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