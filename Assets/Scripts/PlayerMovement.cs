using System.Collections;
using UnityEngine;

/// <summary>
/// Handles all player input, physics, kick window, health, and damage.
///
/// Health values live in PlayerStats (singleton); PlayerMovement drives
/// all state changes (damage, regen, death) and reads health to decide outcomes.
///
/// Input flow:
///   SwipeDetection.SwipePerformed → OnSwipe()
///     Swipe up         → TryJump
///     Swipe down       → TryFastFall
///     Swipe left/right → TryKick
///
/// Kick window:
///   Opens on the OnKickWindowOpen animation event, stays open for
///   kickWindowDuration, then closes. FixedUpdate polls CheckKickContact()
///   every physics tick while the window is open — more reliable than a
///   single event-frame check and tolerant of animation timing variance.
///
/// Shield:
///   GrantShield() activates a one-hit absorber. Both TakeDamage() and
///   InstantKill() check HasShield first: the shield breaks, a short
///   invincibility window opens, and damage is fully absorbed.
///   shieldVisual (optional child GameObject) is toggled automatically.
///
/// Regen:
///   Dormant by default. Call StartRegen() from a consumable to begin
///   filling one heart over lifeRegenInterval seconds.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{
    // ─── Health ───────────────────────────────────────────────────────────────
    [Header("Health")]
    [Tooltip("Seconds to fill one full heart during regen. Used when a regen consumable is active.")]
    [SerializeField] private float lifeRegenInterval = 15f;

    [Tooltip("Invincibility window after a normal hit — prevents chain damage.")]
    [SerializeField] private float hitInvincibilityDuration = 2f;

    // ─── Physics ──────────────────────────────────────────────────────────────
    [Header("Physics Settings")]
    [Tooltip("Upward impulse applied on jump.")]
    [SerializeField] private float jumpForce = 18f;

    [Tooltip("Downward velocity applied on fast-fall.")]
    [SerializeField] private float slamForce = 15f;

    [Tooltip("Extra downward acceleration while airborne (stacks with gravity).")]
    [SerializeField] private float fallGravity = 10f;

    // ─── Actions ──────────────────────────────────────────────────────────────
    [Header("Action Settings")]
    [Tooltip("Minimum seconds between consecutive jumps or kicks.")]
    [SerializeField] private float actionCooldown = 0.2f;

    // ─── Kick ─────────────────────────────────────────────────────────────────
    [Header("Kick Settings")]
    [Tooltip("Overlap-sphere radius used to detect the pipe during the kick window.")]
    [SerializeField] private float kickRange = 1.5f;

    [Tooltip("Downward offset from the player pivot for the kick detection origin.")]
    [SerializeField] private float kickHeightOffset = 0.5f;

    [Tooltip("Layer(s) the pipe collider lives on. Used by the kick overlap sphere.")]
    [SerializeField] private LayerMask pipeLayer;

    [Tooltip("Seconds the kick hit-window stays open after the OnKickWindowOpen animation event.")]
    [SerializeField] private float kickWindowDuration = 0.2f;

    [Tooltip("Invincibility granted from the moment a kick lands.")]
    [SerializeField] private float kickInvincibilityDuration = 0.4f;

    // ─── Shield ───────────────────────────────────────────────────────────────
    [Header("Shield")]
    [Tooltip("Invincibility granted when the shield breaks — prevents immediate follow-up damage.")]
    [SerializeField] private float shieldBreakInvincibilityDuration = 1.5f;

    [Tooltip("Optional child GameObject shown while the shield is active. Leave null to skip.")]
    [SerializeField] private GameObject shieldVisual;

    // ─── Debug (visible in Inspector, not serialized to avoid accidental edits) ─
    [Header("Debug")]
    [SerializeField] private bool isGrounded;

    // Exposed in Inspector for live debugging only — not a config value.
    [SerializeField] private bool _kickWindowOpen;

    // ─── Public State ─────────────────────────────────────────────────────────
    /// <summary>True while a kick animation's active hit-window is open.</summary>
    public bool IsKicking { get; private set; }

    /// <summary>True while any invincibility window (hit, kick, or shield-break) is active.</summary>
    public bool IsInvincible { get; private set; }

    /// <summary>True while the player has an active shield consumable.</summary>
    public bool HasShield { get; private set; }

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

    // Pre-allocated buffer — avoids per-call allocation in FixedUpdate.
    private readonly Collider[] _kickHits = new Collider[4];

    // Cached animator parameter hashes — computed once, never GC-allocated at runtime.
    private static readonly int IsGroundHash = Animator.StringToHash("isGround");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int RollHash = Animator.StringToHash("Roll");
    private static readonly int KickRightHash = Animator.StringToHash("kickRight");
    private static readonly int KickLeftHash = Animator.StringToHash("kickLeft");
    private static readonly int IdleHash = Animator.StringToHash("Idle");

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();

        // Lock all rotation and XZ translation — player only moves on the Y axis.
        _rb.constraints = RigidbodyConstraints.FreezeRotation
                        | RigidbodyConstraints.FreezePositionX
                        | RigidbodyConstraints.FreezePositionZ;

        // Cache the main (non-lethal) pipe. Use SetPipeLogic() to override at runtime.
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

    private void FixedUpdate()
    {
        // Extra downward force while airborne to make jumps feel snappy on mobile.
        if (!isGrounded)
            _rb.AddForce(Vector3.down * fallGravity, ForceMode.Acceleration);

        if (_kickWindowOpen)
            CheckKickContact();
    }

    // ─── Input ────────────────────────────────────────────────────────────────

    private void OnSwipe(Vector2 direction)
    {
        if (direction.y > 0.5f) TryJump();
        else if (direction.y < -0.5f) TryFastFall();
        else if (Mathf.Abs(direction.x) > 0.5f) TryKick(direction);
    }

    // ─── Jump ─────────────────────────────────────────────────────────────────

    private void TryJump()
    {
        if (!isGrounded || Time.time < _lastJumpTime + actionCooldown) return;
        DoJump();
    }

    private void DoJump()
    {
        _lastJumpTime = Time.time;

        // Zero Y velocity before adding the impulse so the full force is always felt.
        Vector3 v = _rb.linearVelocity;
        v.y = 0f;
        _rb.linearVelocity = v;

        _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        _animator.CrossFade(JumpHash, 0.05f);
    }

    // ─── Fast Fall ────────────────────────────────────────────────────────────

    private void TryFastFall()
    {
        if (isGrounded) return;
        DoFastFall();
    }

    private void DoFastFall()
    {
        // Override Y velocity directly — no additive force, instant commitment.
        Vector3 v = _rb.linearVelocity;
        v.y = -slamForce;
        _rb.linearVelocity = v;
        _rb.angularVelocity = Vector3.zero;

        _animator.CrossFade(RollHash, 0.05f);

        Camera.main?.GetComponent<CameraController>()?.TriggerShake(0.08f, 0.12f);
        GameManager.instance?.TriggerHitStop(0.2f, 0.02f);
    }

    // ─── Kick ─────────────────────────────────────────────────────────────────

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
        SoundManager.Instance?.PlaySFX(SoundType.KickAttempt);
        // Hit window opens on the OnKickWindowOpen animation event, not here.
    }

    /// <summary>
    /// Animation event — fires at wind-up completion (foot starts moving).
    /// Opens the kick hit-window so FixedUpdate can detect pipe contact.
    /// </summary>
    public void OnKickWindowOpen()
    {
        _kickLandedThisSwing = false;

        if (_kickWindowRoutine != null) StopCoroutine(_kickWindowRoutine);
        _kickWindowRoutine = StartCoroutine(KickWindowRoutine());
    }

    /// <summary>Animation event — optional early close at follow-through end.</summary>
    public void OnKickWindowClose() => CloseKickWindow();

    /// <summary>Legacy animation event name — safe fallback for old clips.</summary>
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

    /// <summary>
    /// Polled every FixedUpdate tick while the kick window is open.
    /// The multi-frame window makes timing forgiving while still requiring
    /// the correct swipe direction to match the pipe's rotation.
    /// </summary>
    private void CheckKickContact()
    {
        if (_kickLandedThisSwing || _pipe == null) return;

        Vector3 origin = transform.position - new Vector3(0f, kickHeightOffset, 0f);
        int hitCount = Physics.OverlapSphereNonAlloc(origin, kickRange, _kickHits, pipeLayer);
        if (hitCount == 0) return;

        // Direction must match the pipe's current rotation — wrong-direction kicks miss.
        bool validDirection = (_currentKickDirection.x > 0f && _pipe.rotationDirection) ||
                              (_currentKickDirection.x < 0f && !_pipe.rotationDirection);
        if (!validDirection) return;

        if (!_pipe.GetKicked(_currentKickDirection)) return;

        _kickLandedThisSwing = true;
        CloseKickWindow();

        GameManager.instance?.AddBonusScore(1);
        SoundManager.Instance?.PlaySFX(SoundType.KickSuccess);
        GameManager.instance?.TriggerHitStop(0.15f, 0.03f);
        Camera.main?.GetComponent<CameraController>()?.TriggerShake(0.06f, 0.15f);

        // Grant invincibility from kick contact — prevents pipe double-hits.
        if (_invincibilityRoutine != null) StopCoroutine(_invincibilityRoutine);
        _invincibilityRoutine = StartCoroutine(InvincibilityRoutine(kickInvincibilityDuration));
    }

    // ─── Health & Damage ──────────────────────────────────────────────────────

    /// <summary>
    /// Applies damage from the non-lethal pipe.
    /// If a shield is active it absorbs the hit entirely.
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (IsInvincible) return;

        if (HasShield)
        {
            BreakShield();
            SoundManager.Instance?.PlaySFX(SoundType.ShieldBreak);
            Camera.main?.GetComponent<CameraController>()?.TriggerShake(0.1f, 0.25f);
            GameManager.instance?.TriggerHitStop(0.15f, 0.05f);
            return;
        }

        // Kill lateral drift so the player doesn't slide after being hit.
        Vector3 v = _rb.linearVelocity;
        v.x = 0f; v.z = 0f;
        _rb.linearVelocity = v;
        _rb.angularVelocity = Vector3.zero;

        PlayerStats.Instance.TakeDamage(amount);
        SoundManager.Instance?.PlaySFX(SoundType.PlayerDamage);

        if (PlayerStats.Instance.Health < 1f)
        {
            GameManager.instance?.EndGame();
            return;
        }

        if (_invincibilityRoutine != null) StopCoroutine(_invincibilityRoutine);
        _invincibilityRoutine = StartCoroutine(InvincibilityRoutine(hitInvincibilityDuration));
    }

    /// <summary>
    /// Instant death — used by the lethal (second) pipe.
    /// The shield absorbs this hit too; otherwise the game ends immediately.
    /// </summary>
    public void InstantKill()
    {
        if (HasShield)
        {
            BreakShield();
            SoundManager.Instance?.PlaySFX(SoundType.ShieldBreak);
            Camera.main?.GetComponent<CameraController>()?.TriggerShake(0.1f, 0.25f);
            GameManager.instance?.TriggerHitStop(0.15f, 0.05f);
            return;
        }

        SoundManager.Instance?.PlaySFX(SoundType.PlayerDeath);
        GameManager.instance?.EndGame();
    }

    // ─── Regen ────────────────────────────────────────────────────────────────
    // Dormant by default — only activated by a consumable item.
    // Fills one heart at a time; waits if already at full health.

    /// <summary>
    /// Starts (or restarts) the regen loop.
    /// Called by a regen consumable — never on game start.
    /// </summary>
    public void StartRegen() => RestartRegenLoop();

    private IEnumerator RegenLoop()
    {
        while (true)
        {
            // Wait if already full.
            if (PlayerStats.Instance.Health >= PlayerStats.Instance.MaxHealth)
                yield return new WaitUntil(() => PlayerStats.Instance.Health < PlayerStats.Instance.MaxHealth);

            // Heal toward the next full integer heart.
            float target = Mathf.Min(
                Mathf.Floor(PlayerStats.Instance.Health) + 1f,
                PlayerStats.Instance.MaxHealth
            );

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

    // ─── Shield ───────────────────────────────────────────────────────────────

    /// <summary>Grants the player a one-hit shield and activates the shield visual.</summary>
    public void GrantShield()
    {
        HasShield = true;
        if (shieldVisual != null) shieldVisual.SetActive(true);
    }

    /// <summary>
    /// Breaks the shield and opens a brief invincibility window so a follow-up
    /// hit in the same physics frame cannot immediately drain health.
    /// </summary>
    public void BreakShield()
    {
        HasShield = false;
        if (shieldVisual != null) shieldVisual.SetActive(false);

        if (_invincibilityRoutine != null) StopCoroutine(_invincibilityRoutine);
        _invincibilityRoutine = StartCoroutine(InvincibilityRoutine(shieldBreakInvincibilityDuration));
    }

    // ─── Ground Detection ─────────────────────────────────────────────────────

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

    // ─── Coroutines ───────────────────────────────────────────────────────────

    /// <summary>
    /// Unified invincibility coroutine shared by hit, kick, and shield-break paths.
    /// Consolidates three near-identical coroutines into one parameterised version.
    /// </summary>
    private IEnumerator InvincibilityRoutine(float duration)
    {
        IsInvincible = true;
        yield return new WaitForSeconds(duration);
        IsInvincible = false;
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>Overrides the cached pipe reference (e.g. after scene setup).</summary>
    public void SetPipeLogic(PipeLogic targetPipe) => _pipe = targetPipe;

    /// <summary>Called by kickBehaviour StateMachineBehaviour to sync IsKicking.</summary>
    public void SetKickState(bool value) => IsKicking = value;

    // ─── Gizmos ───────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _kickWindowOpen ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(
            transform.position - new Vector3(0f, kickHeightOffset, 0f),
            kickRange
        );
    }
}