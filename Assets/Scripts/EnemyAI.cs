using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls a single enemy's decision-making, movement, kick timing, health,
/// and world-space heart HUD.
///
/// Kick timing system:
///   When DecideAction() selects a kick, TryKick() calculates how many seconds
///   until the rotating pipe tip reaches this enemy's angular position.
///   The animation starts (arrivalTime - kickWindUpDuration) seconds early so
///   that the OnKickWindowOpen event fires exactly when the pipe arrives.
///
///   Calculation:
///     tipAngle   = XZ angle of the pipe tip relative to the pipe center
///     enemyAngle = XZ angle of this enemy relative to the pipe center
///     gap        = angular distance the tip must travel (in the rotation direction)
///     arrivalTime = gap / runtimeSpeed  (degrees ÷ degrees-per-second = seconds)
///     windUpDelay = arrivalTime - kickWindUpDuration
///
///   If windUpDelay < minTimeToKick the pipe arrives before the animation can
///   wind up, so the enemy jumps instead.
///
/// Heart HUD:
///   Heart containers are instantiated in Awake using the prefab and heartsParent.
///   The HUD billboards toward the main camera every LateUpdate.
///   onHealthChangedCallback is fired after every health change and drives the fill.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public class EnemyAI : MonoBehaviour
{
    // ─── Identity ─────────────────────────────────────────────────────────────
    /// <summary>Set by SpawnManager when this enemy is selected from a boss config.</summary>
    [HideInInspector] public bool isBoss = false;

    // ─── Health ───────────────────────────────────────────────────────────────
    [Header("Health")]
    [SerializeField] private float health;
    [SerializeField] private float maxHealth;

    [Tooltip("Absolute upper bound for maxHealth. Sizes the heart-container array.")]
    [SerializeField] private float maxTotalHealth;

    public float Health { get => health; set => health = value; }
    public float MaxHealth { get => maxHealth; set => maxHealth = value; }
    public float MaxTotalHealth => maxTotalHealth;

    /// <summary>Fired after every health change. HealthBarController subscribes here.</summary>
    public event System.Action onHealthChangedCallback;

    // ─── Heart Display ────────────────────────────────────────────────────────
    [Header("Heart Display")]
    [Tooltip("Parent transform for the instantiated heart containers (world-space canvas).")]
    public Transform heartsParent;

    [Tooltip("Prefab with a child Image named 'HeartFill' (Image type = Filled).")]
    public GameObject heartContainerPrefab;

    private GameObject[] _heartContainers;
    private Image[] _heartFills;

    // ─── State ────────────────────────────────────────────────────────────────
    /// <summary>True while a kick animation's active hit-window is open.</summary>
    [HideInInspector] public bool isKicking = false;

    /// <summary>True during kick wind-up invincibility or any other i-frame window.</summary>
    [HideInInspector] public bool isInvincible = false;

    // ─── Jump ─────────────────────────────────────────────────────────────────
    [Header("Jump")]
    [Tooltip("Upward impulse applied when the enemy dodges.")]
    [SerializeField] private float jumpForce = 18f;

    // ─── Kick Settings ────────────────────────────────────────────────────────
    [Header("Kick Settings")]
    [Tooltip("Overlap-sphere radius for the kick contact check.")]
    [SerializeField] private float kickRange = 1.2f;

    [Tooltip("Layer(s) the pipe lives on. Used by the kick overlap sphere.")]
    [SerializeField] private LayerMask pipeLayer;

    [Tooltip("Transform at the enemy's foot / kick point. Falls back to transform.position.")]
    [SerializeField] private Transform kickPoint;

    [Tooltip("Seconds from animation start to the OnKickWindowOpen event. " +
             "Measure this in the Animator. Critical: subtracted from arrivalTime " +
             "to determine when to start the animation.")]
    [SerializeField] private float kickWindUpDuration = 0.2f;

    [Tooltip("Seconds the hit-window stays open. Should match the active frames in the kick clip.")]
    [SerializeField] private float kickWindowDuration = 0.15f;

    [Tooltip("Invincibility granted from OnKickWindowOpen. Should cover the full kick animation.")]
    [SerializeField] private float kickInvincibilityDuration = 0.5f;

    [Tooltip("If arrivalTime - kickWindUpDuration < this, jump instead of kick.")]
    [SerializeField] private float minTimeToKick = 0.1f;

    // ─── AI Decisions ─────────────────────────────────────────────────────────
    [Header("AI Decisions")]
    [Range(0f, 1f)]
    [Tooltip("Kick probability at difficulty 0 (game start).")]
    [SerializeField] private float kickChanceAtMinDifficulty = 0.3f;

    [Range(0f, 1f)]
    [Tooltip("Kick probability at difficulty 1 (max).")]
    [SerializeField] private float kickChanceAtMaxDifficulty = 0.75f;

    [Range(0f, 1f)]
    [Tooltip("Hesitate probability at difficulty 0 — gives the player breathing room early on.")]
    [SerializeField] private float hesitateChanceAtMinDifficulty = 0.3f;

    [Range(0f, 1f)]
    [Tooltip("Hesitate probability at difficulty 1 — enemies react faster at max difficulty.")]
    [SerializeField] private float hesitateChanceAtMaxDifficulty = 0f;

    // ─── Private ──────────────────────────────────────────────────────────────
    private PipeLogic _pipe;
    private Animator _animator;
    private Rigidbody _rb;
    private Camera _mainCamera;

    /// <summary>Snapshotted at the moment DecideAction() selects a kick — never mutated after that.</summary>
    private Vector2 _committedKickDirection;
    private bool _kickWindowOpen;
    private bool _kickLandedThisSwing;
    private bool _isDead;

    private Coroutine _kickWindowRoutine;
    private Coroutine _invincibilityRoutine;

    // Pre-allocated buffer for OverlapSphereNonAlloc — zero GC during FixedUpdate.
    private readonly Collider[] _kickHits = new Collider[4];

    [SerializeField] private bool isGrounded;

    // Cached animator parameter hashes — avoids string hashing at runtime.
    private static readonly int IsGroundHash = Animator.StringToHash("isGround");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int IdleHash = Animator.StringToHash("Idle");
    private static readonly int KickRightHash = Animator.StringToHash("kickRight");
    private static readonly int KickLeftHash = Animator.StringToHash("kickLeft");

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        // Find the non-lethal pipe — enemies only react to the main pipe.
        foreach (PipeLogic p in FindObjectsByType<PipeLogic>(FindObjectsInactive.Include))
        {
            if (!p.isLethalPipe) { _pipe = p; break; }
        }

        _animator = GetComponent<Animator>();
        _rb = GetComponent<Rigidbody>();
        _mainCamera = Camera.main;

        InstantiateHeartContainers();
        onHealthChangedCallback += UpdateHeartsHUD;
        UpdateHeartsHUD();
    }

    private void FixedUpdate()
    {
        // Poll kick contact every physics tick while the window is open.
        if (_kickWindowOpen)
            CheckKickContact();
    }

    private void LateUpdate()
    {
        // Billboard the heart HUD toward the camera every frame.
        if (_mainCamera != null && heartsParent != null)
        {
            heartsParent.LookAt(
                heartsParent.position + _mainCamera.transform.rotation * Vector3.forward,
                _mainCamera.transform.rotation * Vector3.up
            );
        }
    }

    private void OnDestroy()
    {
        onHealthChangedCallback -= UpdateHeartsHUD;
    }

    // ─── AI Decision ──────────────────────────────────────────────────────────

    /// <summary>
    /// Called by EnemyTriggerArea after the reaction delay expires.
    /// Rolls kick / jump / hesitate based on current difficulty.
    /// </summary>
    public void DecideAction()
    {
        if (_isDead) return;

        float difficulty = GameManager.instance != null ? GameManager.instance.DifficultyNormalized : 0f;
        float kickChance = Mathf.Clamp01(Mathf.Lerp(kickChanceAtMinDifficulty, kickChanceAtMaxDifficulty, difficulty));
        float hesitateChance = Mathf.Clamp01(Mathf.Lerp(hesitateChanceAtMinDifficulty, hesitateChanceAtMaxDifficulty, difficulty));

        float roll = Random.value;

        if (roll < kickChance) TryKick();
        else if (roll < 1f - hesitateChance) DoJump();
        // else: hesitate — do nothing this sweep
    }

    // ─── Movement ─────────────────────────────────────────────────────────────

    private void DoJump()
    {
        if (!isGrounded) return;

        Vector3 v = _rb.linearVelocity;
        v.y = 0f;
        _rb.linearVelocity = v;

        _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        _animator.CrossFade(JumpHash, 0.05f);
    }

    // ─── Kick — Arrival Timing ────────────────────────────────────────────────

    /// <summary>
    /// Calculates pipe arrival time and schedules the animation so
    /// OnKickWindowOpen fires exactly when the pipe tip reaches this enemy.
    /// Falls back to DoJump if there is not enough time to wind up.
    /// </summary>
    private void TryKick()
    {
        if (_pipe == null) return;

        // Snapshot the direction NOW — pipe direction may change before the animation starts.
        _committedKickDirection = _pipe.rotationDirection ? Vector2.right : Vector2.left;

        float windUpDelay = CalculatePipeArrivalTime() - kickWindUpDuration;
        if (windUpDelay < minTimeToKick)
        {
            DoJump();
            return;
        }

        StartCoroutine(TimedKickSequence(windUpDelay));
    }

    /// <summary>
    /// Waits windUpDelay seconds then plays the kick animation.
    /// The animation's OnKickWindowOpen event drives the rest.
    /// </summary>
    private IEnumerator TimedKickSequence(float windUpDelay)
    {
        yield return new WaitForSeconds(windUpDelay);
        if (_isDead) yield break;

        _animator.CrossFade(
            _committedKickDirection == Vector2.right ? KickRightHash : KickLeftHash,
            0.02f
        );
        SoundManager.Instance?.PlaySFX(SoundType.KickAttempt);
    }

    /// <summary>
    /// Calculates seconds until the pipe tip reaches this enemy's XZ angle,
    /// travelling in the current rotation direction.
    /// Returns a safe fallback if the pipe or tip reference is missing.
    /// </summary>
    private float CalculatePipeArrivalTime()
    {
        if (_pipe == null || _pipe.pipeTip == null)
            return kickWindUpDuration + minTimeToKick;

        Vector3 pipeCenter = _pipe.transform.position;

        Vector3 tipRelative = _pipe.pipeTip.position - pipeCenter; tipRelative.y = 0f;
        Vector3 enemyRelative = transform.position - pipeCenter; enemyRelative.y = 0f;

        if (tipRelative.sqrMagnitude < 0.001f || enemyRelative.sqrMagnitude < 0.001f)
            return kickWindUpDuration + minTimeToKick;

        float tipAngle = Mathf.Atan2(tipRelative.z, tipRelative.x) * Mathf.Rad2Deg;
        float enemyAngle = Mathf.Atan2(enemyRelative.z, enemyRelative.x) * Mathf.Rad2Deg;

        // Normalise gap to [0, 360) in the pipe's travel direction.
        float gap = _pipe.rotationDirection
            ? enemyAngle - tipAngle   // CCW: tip chases enemy forward in angle
            : tipAngle - enemyAngle;// CW:  tip chases enemy backward in angle

        gap = ((gap % 360f) + 360f) % 360f;

        return gap / Mathf.Max(_pipe.RuntimeSpeed, 1f);
    }

    // ─── Kick Window — Animation Event Driven ────────────────────────────────

    /// <summary>
    /// Animation event — fires at wind-up completion (foot starts moving forward).
    /// Opens the hit window and grants kick invincibility.
    /// </summary>
    public void OnKickWindowOpen()
    {
        if (_isDead) return;

        _kickLandedThisSwing = false;

        if (_invincibilityRoutine != null) StopCoroutine(_invincibilityRoutine);
        _invincibilityRoutine = StartCoroutine(KickInvincibilityRoutine());

        if (_kickWindowRoutine != null) StopCoroutine(_kickWindowRoutine);
        _kickWindowRoutine = StartCoroutine(KickWindowRoutine());
    }

    /// <summary>Animation event — optional early close at follow-through end.</summary>
    public void OnKickWindowClose() => CloseKickWindow();

    /// <summary>Legacy stub — kept for animation clip compatibility.</summary>
    public void OnKickImpact() { }

    private IEnumerator KickWindowRoutine()
    {
        isKicking = true;
        _kickWindowOpen = true;

        yield return new WaitForSeconds(kickWindowDuration);

        CloseKickWindow();
    }

    private void CloseKickWindow()
    {
        _kickWindowOpen = false;
        isKicking = false;

        if (_kickWindowRoutine != null) { StopCoroutine(_kickWindowRoutine); _kickWindowRoutine = null; }
    }

    /// <summary>
    /// Polled every FixedUpdate while the kick window is open.
    /// Uses the direction snapshotted at decision time.
    /// </summary>
    private void CheckKickContact()
    {
        if (_kickLandedThisSwing || _pipe == null) return;

        Vector3 origin = kickPoint != null ? kickPoint.position : transform.position;
        int hitCount = Physics.OverlapSphereNonAlloc(origin, kickRange, _kickHits, pipeLayer);
        if (hitCount == 0) return;

        if (!_pipe.GetKicked(_committedKickDirection)) return;

        _kickLandedThisSwing = true;
        CloseKickWindow();

        GameManager.instance?.TriggerHitStop(0.15f, 0.03f);
        Camera.main?.GetComponent<CameraController>()?.TriggerShake(0.06f, 0.15f);
    }

    private IEnumerator KickInvincibilityRoutine()
    {
        isInvincible = true;
        yield return new WaitForSeconds(kickInvincibilityDuration);
        isInvincible = false;
    }

    // ─── Health ───────────────────────────────────────────────────────────────

    /// <summary>Reduces health by amount. Triggers death when health drops below 1.</summary>
    public void TakeDamage(int amount)
    {
        if (_isDead || isInvincible) return;

        health -= amount;
        ClampHealth();
        SoundManager.Instance?.PlaySFX(SoundType.EnemyDamage);

        if (health < 1f) Die();
    }

    /// <summary>
    /// Instant kill — used by the lethal pipe.
    /// Bypasses invincibility; no death animation, just removes the enemy.
    /// </summary>
    public void InstantKill()
    {
        if (_isDead) return;
        _isDead = true;
        SpawnManager.instance.OnEnemyDied(gameObject);
    }

    /// <summary>Restores health by amount, clamped to maxHealth.</summary>
    public void Heal(float amount)
    {
        health += amount;
        ClampHealth();
    }

    private void ClampHealth()
    {
        health = Mathf.Clamp(health, 0f, maxHealth);
        onHealthChangedCallback?.Invoke();
    }

    private void Die()
    {
        _isDead = true;
        SoundManager.Instance?.PlaySFX(SoundType.EnemyDeath);
        SpawnManager.instance.OnEnemyDied(gameObject);
    }

    // ─── Heart Display ────────────────────────────────────────────────────────

    private void InstantiateHeartContainers()
    {
        if (heartContainerPrefab == null || heartsParent == null) return;

        int total = Mathf.RoundToInt(maxTotalHealth);
        _heartContainers = new GameObject[total];
        _heartFills = new Image[total];

        for (int i = 0; i < total; i++)
        {
            GameObject container = Instantiate(heartContainerPrefab, heartsParent, false);
            _heartContainers[i] = container;

            Transform fill = container.transform.Find("HeartFill");
            if (fill != null)
                _heartFills[i] = fill.GetComponent<Image>();
            else
                Debug.LogWarning($"[EnemyAI] Heart prefab slot {i} missing 'HeartFill' child.");
        }
    }

    /// <summary>Refreshes the heart HUD to match current health values.</summary>
    public void UpdateHeartsHUD()
    {
        SetHeartContainers();
        SetFilledHearts();
    }

    private void SetHeartContainers()
    {
        if (_heartContainers == null) return;
        for (int i = 0; i < _heartContainers.Length; i++)
            _heartContainers[i]?.SetActive(i < maxHealth);
    }

    private void SetFilledHearts()
    {
        if (_heartFills == null) return;

        for (int i = 0; i < _heartFills.Length; i++)
        {
            if (_heartFills[i] == null) continue;
            _heartFills[i].fillAmount = i < health ? 1f : 0f;
        }

        // Handle fractional (partial) heart.
        if (health % 1f != 0f)
        {
            int partialSlot = Mathf.FloorToInt(health);
            if (partialSlot < _heartFills.Length && _heartFills[partialSlot] != null)
                _heartFills[partialSlot].fillAmount = health % 1f;
        }
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

    // ─── Gizmos ───────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isBoss ? Color.yellow : Color.red;
        Gizmos.DrawWireSphere(kickPoint != null ? kickPoint.position : transform.position, kickRange);
    }
}