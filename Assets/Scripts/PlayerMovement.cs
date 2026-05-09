using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Handles all player input, physics actions, kick window, lives system, and damage.
///
/// Input flow (via SwipeDetection):
///   Swipe up    → TryJump
///   Swipe down  → TryFastFall
///   Swipe left/right → TryKick
///
/// Kick window:
///   DoKick opens _kickWindowOpen immediately on swipe.
///   FixedUpdate polls CheckKickContact() every physics tick while open.
///   One landed hit per swing (_kickLandedThisSwing guard) — window closes on success.
///
/// Lives system:
///   TakeDamage() drains lives and starts a hit invincibility window.
///   LivesRegenLoop() passively ticks regen — restarted from zero after damage.
///   OnLivesChanged fires every frame during regen (float = 0→1 fill progress)
///   and on instant changes (float = 0) — UI subscribes to this.
///
/// Fast fall feel:
///   Extra gravity multiplier while IsFastFalling.
///   Hard animation snap (Play not CrossFade) for instant visual commitment.
///   OnFastFallLand(): freeze frame + OnFastFallLanded event → CameraController.TriggerShake.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{
    // ─── Lives ────────────────────────────────────────────────────────────────
    [Header("Lives")]
    [SerializeField] private int maxLives = 3;
    [Tooltip("Seconds to regenerate one life.")]
    [SerializeField] private float lifeRegenInterval = 15f;
    [Tooltip("Invincibility window after taking a hit. Prevents chain damage.")]
    [SerializeField] private float hitInvincibilityDuration = 2f;

    // ─── Physics ──────────────────────────────────────────────────────────────
    [Header("Physics Settings")]
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private float slamForce = 18f;
    [Tooltip("Extra downward force applied every FixedUpdate while airborne.")]
    [SerializeField] private float fallGravity = 30f;

    // ─── Fast Fall ────────────────────────────────────────────────────────────
    [Header("Fast Fall Feel")]
    [Tooltip("Multiplied against fallGravity while fast-falling. 2-3 feels punchy.")]
    [SerializeField] private float fastFallGravityMultiplier = 2.5f;
    [Tooltip("TimeScale during landing freeze. Near-zero = impact freeze frame.")]
    [SerializeField] private float landingFreezeTimeScale = 0.05f;
    [Tooltip("Real-time seconds the freeze lasts (~1-2 frames at 60fps = 0.02-0.04).")]
    [SerializeField] private float landingFreezeDuration = 0.06f;
    [Tooltip("Shake magnitude passed to CameraController on landing.")]
    [SerializeField] private float landingShakeMagnitude = 0.15f;
    [Tooltip("Shake duration passed to CameraController on landing.")]
    [SerializeField] private float landingShakeDuration = 0.12f;

    // ─── Actions ──────────────────────────────────────────────────────────────
    [Header("Action Settings")]
    [Tooltip("Minimum seconds between consecutive actions of the same type.")]
    [SerializeField] private float actionCooldown = 0.2f;

    // ─── Kick ─────────────────────────────────────────────────────────────────
    [Header("Kick Settings")]
    [SerializeField] private float kickRange = 1.5f;
    [Tooltip("Downward offset from pivot to kick origin sphere center.")]
    [SerializeField] private float kickHeightOffset = 0.5f;
    [SerializeField] private LayerMask pipeLayer;
    [Tooltip("How long the kick hit window stays open after swipe. Increase for more forgiveness.")]
    [SerializeField] private float kickWindowDuration = 0.2f;
    [SerializeField] private float kickInvincibilityDuration = 0.4f;

    // ─── Debug ────────────────────────────────────────────────────────────────
    [Header("Debug")]
    [SerializeField] private bool isGrounded;
    [SerializeField] private bool _kickWindowOpen;  // Visible in Inspector for live tuning

    // ─── Public State ─────────────────────────────────────────────────────────
    public int CurrentLives { get; private set; }
    public bool IsKicking { get; private set; }
    public bool IsInvincible { get; private set; }
    public bool IsFastFalling { get; private set; }

    // Shake values exposed so CameraController can read them without hardcoding
    public float LandingShakeMagnitude => landingShakeMagnitude;
    public float LandingShakeDuration => landingShakeDuration;

    /// <summary>
    /// Fired on every regen tick (int lives, float progress 0→1)
    /// and on instant life changes (float = 0).
    /// UI subscribes to drive hearts and regen fill ring.
    /// </summary>
    public event Action<int, float> OnLivesChanged;

    /// <summary>Fired when player lands from a fast fall. CameraController subscribes for shake.</summary>
    public event Action OnFastFallLanded;

    // ─── Private ──────────────────────────────────────────────────────────────
    private Rigidbody _rb;
    private Animator _animator;
    private PipeLogic _pipe;

    private Vector2 _currentKickDirection;
    private float _lastJumpTime;
    private float _lastKickTime;
    private bool _kickLandedThisSwing;   // One hit per swing — prevents window multi-scoring

    private Coroutine _invincibilityRoutine;
    private Coroutine _kickWindowRoutine;
    private Coroutine _regenRoutine;
    private Coroutine _landingFreezeRoutine;

    // Pre-allocated overlap buffer — avoids GC allocation every FixedUpdate tick
    private readonly Collider[] _kickHits = new Collider[4];

    // Animator hashes — computed once, never allocate strings at runtime
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

        // Lock X/Z position and all rotation — player only moves on Y axis
        _rb.constraints = RigidbodyConstraints.FreezeRotation
                        | RigidbodyConstraints.FreezePositionX
                        | RigidbodyConstraints.FreezePositionZ;

        _pipe = FindAnyObjectByType<PipeLogic>();
        CurrentLives = maxLives;
    }

    private void OnEnable()
    {
        // Subscribe in OnEnable / unsubscribe in OnDisable — correct pattern for events
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
            // Extra gravity while airborne — multiplied further during fast fall
            float gravity = fallGravity * (IsFastFalling ? fastFallGravityMultiplier : 1f);
            _rb.AddForce(Vector3.down * gravity, ForceMode.Acceleration);
        }

        // Kick window poll — runs every physics tick, much more reliable than a single event frame
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

        // Set v.y to platform speed before adding jump impulse.
        // Without this, zeroing v.y fights the platform's upward momentum
        // and jump height shrinks as platform speed increases.
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
        // Drive v.y to a fixed value relative to the platform rather than applying a downward impulse.
        // This makes slam distance consistent regardless of how fast the platform is rising.
        float platformVY = GameManager.instance != null ? GameManager.instance.CurrentRiseSpeed : 0f;
        Vector3 v = _rb.linearVelocity;
        v.y = platformVY - slamForce;
        _rb.linearVelocity = v;
        _rb.angularVelocity = Vector3.zero;

        IsFastFalling = true;

        // Hard snap to roll — CrossFade would blend lazily; Play commits immediately
        _animator.Play(RollHash, 0, 0f);
    }

    /// <summary>Called from OnCollisionStay when IsFastFalling is true on first ground contact.</summary>
    private void OnFastFallLand()
    {
        IsFastFalling = false;

        // Use SlamLand if it exists — short squash/recover pose; falls back to Idle silently
        _animator.Play(_animator.HasState(0, SlamLandHash) ? SlamLandHash : IdleHash, 0, 0f);

        // Freeze frame — near-zero timeScale for ~1-2 frames gives impact weight
        if (_landingFreezeRoutine != null) StopCoroutine(_landingFreezeRoutine);
        _landingFreezeRoutine = StartCoroutine(LandingFreeze());

        // Notify camera — CameraController.OnFastFallLanded calls TriggerShake()
        OnFastFallLanded?.Invoke();
    }

    private IEnumerator LandingFreeze()
    {
        float saved = Time.timeScale;
        Time.timeScale = landingFreezeTimeScale;

        // WaitForSecondsRealtime ignores timeScale — so the freeze doesn't freeze itself
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

        // Open window immediately — swipe gesture is the player's commitment
        if (_kickWindowRoutine != null) StopCoroutine(_kickWindowRoutine);
        _kickWindowRoutine = StartCoroutine(KickWindowRoutine());
    }

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
    /// Called every FixedUpdate tick while _kickWindowOpen.
    /// Validates direction against live pipe state — still requires correct direction
    /// but the multi-frame window makes timing far more forgiving.
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

        _kickLandedThisSwing = true;   // Lock — no double-scoring this swing
        CloseKickWindow();              // Close early on success

        GameManager.instance.AddBonusScore(1);

        if (_invincibilityRoutine != null) StopCoroutine(_invincibilityRoutine);
        _invincibilityRoutine = StartCoroutine(KickInvincibility());
    }

    // Animation event compatibility stubs
    public void OnKickImpact() => CheckKickContact();
    public void OnKickWindowOpen() => IsKicking = true;
    public void OnKickWindowClose() => CloseKickWindow();

    #endregion

    #region Lives & Damage

    public void TakeDamage(int amount)
    {
        if (IsInvincible) return;

        // Kill lateral drift on hit — player shouldn't slide sideways
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

        // Grant grace window — prevents immediate chain damage after first hit
        if (_invincibilityRoutine != null) StopCoroutine(_invincibilityRoutine);
        _invincibilityRoutine = StartCoroutine(HitInvincibility());

        // Reset regen — player must survive a full interval before recovering
        RestartRegenLoop();
    }

    /// <summary>
    /// Passive regen loop — runs forever, broadcasts fill progress every frame.
    /// Restarted from zero by TakeDamage so the timer resets on each hit.
    /// </summary>
    private IEnumerator LivesRegenLoop()
    {
        while (true)
        {
            // Sleep until a life is missing — no wasted ticks at full health
            if (CurrentLives >= maxLives)
                yield return new WaitUntil(() => CurrentLives < maxLives);

            // Tick progress 0→1 over lifeRegenInterval, broadcasting every frame for UI fill
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

    /// <summary>Fires OnLivesChanged with progress = 0 (instant change, not a regen tick).</summary>
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

        if (!isGrounded)
        {
            // First frame of landing — route to correct handler
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

    /// <summary>Called by SpawnManager or level setup if a new pipe is introduced mid-run.</summary>
    public void SetPipeLogic(PipeLogic targetPipe) => _pipe = targetPipe;

    /// <summary>Called by kickBehaviour StateMachineBehaviour to sync animation state.</summary>
    public void SetKickState(bool value) => IsKicking = value;

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        // Green = window open, Cyan = closed — useful in play mode for timing tuning
        Gizmos.color = _kickWindowOpen ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(transform.position - new Vector3(0f, kickHeightOffset, 0f), kickRange);
    }

    #endregion
}