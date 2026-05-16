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

    // ─── Fast Fall ────────────────────────────────────────────────────────────
    [Header("Fast Fall Feel")]
    [Tooltip("Extra gravity multiplier applied only while fast-falling. 2-3 feels punchy.")]
    [SerializeField] private float fastFallGravityMultiplier = 2.5f;
    [Tooltip("Near-zero timeScale for ~1 frame on landing. Gives impact weight.")]
    [SerializeField] private float landingFreezeTimeScale = 0.05f;
    [Tooltip("Real-time seconds the freeze lasts. 0.04-0.06 = 2-3 frames at 60fps.")]
    [SerializeField] private float landingFreezeDuration = 0.05f;

    // ─── Actions ──────────────────────────────────────────────────────────────
    [Header("Action Settings")]
    [SerializeField] private float actionCooldown = 0.2f;

    // ─── Kick ─────────────────────────────────────────────────────────────────
    [Header("Kick Settings")]
    [SerializeField] private float kickRange = 1.5f;
    [SerializeField] private float kickHeightOffset = 0.5f;
    [SerializeField] private LayerMask pipeLayer;
    [SerializeField] private float kickWindowDuration = 0.2f;
    [SerializeField] private float kickInvincibilityDuration = 0.4f;

    // ─── Debug ────────────────────────────────────────────────────────────────
    [Header("Debug")]
    [SerializeField] private bool isGrounded;
    [SerializeField] private bool _kickWindowOpen;

    // ─── Public State ─────────────────────────────────────────────────────────
    public bool IsKicking { get; private set; }
    public bool IsInvincible { get; private set; }
    public bool IsFastFalling { get; private set; }

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
    private Coroutine _landingFreezeRoutine;

    private readonly Collider[] _kickHits = new Collider[4];

    private static readonly int IsGroundHash = Animator.StringToHash("isGround");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int RollHash = Animator.StringToHash("Roll");
    private static readonly int KickRightHash = Animator.StringToHash("kickRight");
    private static readonly int KickLeftHash = Animator.StringToHash("kickLeft");
    private static readonly int IdleHash = Animator.StringToHash("Idle");
    private static readonly int SlamLandHash = Animator.StringToHash("SlamLand");

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
        _regenRoutine = StartCoroutine(LivesRegenLoop());
    }

    private void FixedUpdate()
    {
        if (!isGrounded)
        {
            // Multiply gravity during fast fall — makes the slam feel heavy and committed
            float gravity = fallGravity * (IsFastFalling ? fastFallGravityMultiplier : 1f);
            _rb.AddForce(Vector3.down * gravity, ForceMode.Acceleration);
        }

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

        float platformVY = GameManager.instance != null ? GameManager.instance.CurrentRiseSpeed : 0f;
        Vector3 v = _rb.linearVelocity;
        v.y = platformVY;
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
        float platformVY = GameManager.instance != null ? GameManager.instance.CurrentRiseSpeed : 0f;
        Vector3 v = _rb.linearVelocity;
        v.y = platformVY - slamForce;
        _rb.linearVelocity = v;
        _rb.angularVelocity = Vector3.zero;

        IsFastFalling = true;

        // Hard snap — no blend time so the character visually commits instantly
        _animator.Play(RollHash, 0, 0f);
    }

    /// <summary>Called from OnCollisionStay on first ground contact while IsFastFalling.</summary>
    private void OnFastFallLand()
    {
        IsFastFalling = false;

        // Play SlamLand if it exists, otherwise snap to Idle
        bool hasSlamLand = _animator.HasState(0, SlamLandHash);
        _animator.Play(hasSlamLand ? SlamLandHash : IdleHash, 0, 0f);

        // Freeze frame — near-zero timeScale for a blink gives weight to the impact
        if (_landingFreezeRoutine != null) StopCoroutine(_landingFreezeRoutine);
        _landingFreezeRoutine = StartCoroutine(LandingFreeze());
    }

    private IEnumerator LandingFreeze()
    {
        float saved = Time.timeScale;
        Time.timeScale = landingFreezeTimeScale;
        // WaitForSecondsRealtime ignores timeScale — so the freeze doesn't pause itself
        yield return new WaitForSecondsRealtime(landingFreezeDuration);
        Time.timeScale = saved;
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

        _animator.CrossFade(direction.x > 0f ? KickRightHash : KickLeftHash, 0.03f);
    }

    public void OnKickWindowOpen()
    {
        _kickLandedThisSwing = false;
        if (_kickWindowRoutine != null) StopCoroutine(_kickWindowRoutine);
        _kickWindowRoutine = StartCoroutine(KickWindowRoutine());
    }

    public void OnKickWindowClose() => CloseKickWindow();
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

        PlayerStats.Instance.TakeDamage(amount);

        if (PlayerStats.Instance.Health < 1f)
        {
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
            if (PlayerStats.Instance.Health >= maxLives)
                yield return new WaitUntil(() => PlayerStats.Instance.Health < maxLives);

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
        _regenRoutine = StartCoroutine(LivesRegenLoop());
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

        if (!isGrounded)
        {
            // First frame touching ground — route to correct landing handler
            if (IsFastFalling) OnFastFallLand();
            else _animator.CrossFade(IdleHash, 0.05f);
        }

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