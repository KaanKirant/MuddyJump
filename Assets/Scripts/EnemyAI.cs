using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls a single enemy's decision-making, movement, kick logic, and health.
///
/// Kick timing system:
///   When DecideAction() chooses to kick, TryKick() calculates exactly how
///   many seconds until the rotating pipe tip reaches the enemy's position.
///   The kick animation starts (arrivalTime - kickWindUpDuration) seconds early
///   so that OnKickWindowOpen fires precisely when the pipe arrives.
///
///   Calculation:
///     - tipAngle   = XZ angle of pipe tip relative to pipe center
///     - enemyAngle = XZ angle of enemy relative to pipe center
///     - angularGap = degrees the tip must travel to reach the enemy (in rotation direction)
///     - arrivalTime = angularGap / pipe.CurrentSpeed (degrees per second)
///     - windUpDelay = arrivalTime - kickWindUpDuration
///
///   If the pipe arrives before the animation can wind up (windUpDelay < 0),
///   the kick is skipped and the enemy jumps instead.
///
///   OnKickWindowOpen / OnKickWindowClose are animation events — set them on
///   the kick clips exactly as you did for the player.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public class EnemyAI : MonoBehaviour
{
    // ─── Identity ─────────────────────────────────────────────────────────────
    [HideInInspector] public bool isBoss = false;

    // ─── Health ───────────────────────────────────────────────────────────────
    [Header("Health")]
    [SerializeField] private float health;
    [SerializeField] private float maxHealth;
    [SerializeField] private float maxTotalHealth;

    public float Health { get => health; set => health = value; }
    public float MaxHealth { get => maxHealth; set => maxHealth = value; }
    public float MaxTotalHealth => maxTotalHealth;

    public delegate void OnHealthChangedDelegate();
    public OnHealthChangedDelegate onHealthChangedCallback;

    // ─── Heart Display ────────────────────────────────────────────────────────
    [Header("Heart Display")]
    [Tooltip("Parent transform for instantiated heart containers.")]
    public Transform heartsParent;
    [Tooltip("Prefab with a child Image named 'HeartFill' (Filled type).")]
    public GameObject heartContainerPrefab;

    private GameObject[] _heartContainers;
    private Image[] _heartFills;

    // ─── State ────────────────────────────────────────────────────────────────
    [HideInInspector] public bool isKicking = false;
    [HideInInspector] public bool isInvincible = false;

    // ─── Jump ─────────────────────────────────────────────────────────────────
    [Header("Jump")]
    [SerializeField] private float jumpForce = 18f;

    // ─── Kick Settings ────────────────────────────────────────────────────────
    [Header("Kick Settings")]
    [SerializeField] private float kickRange = 1.2f;
    [SerializeField] private LayerMask pipeLayer;
    [SerializeField] private Transform kickPoint;

    [Tooltip("Seconds from animation start to OnKickWindowOpen event. " +
             "Measure this in the Animator — it is the wind-up duration of the kick clip. " +
             "This value is critical: the system subtracts it from arrival time to know " +
             "when to start the animation so the window opens exactly when the pipe arrives.")]
    [SerializeField] private float kickWindUpDuration = 0.2f;

    [Tooltip("How long the kick window stays open (seconds). " +
             "Should match the active frames in your kick animation.")]
    [SerializeField] private float kickWindowDuration = 0.15f;

    [Tooltip("Invincibility granted from window-open until this many seconds after. " +
             "Should cover the full kick animation length.")]
    [SerializeField] private float kickInvincibilityDuration = 0.5f;

    [Tooltip("If the pipe arrives sooner than this, skip the kick and jump instead. " +
             "Prevents the enemy trying to kick when there is not enough time to wind up.")]
    [SerializeField] private float minTimeToKick = 0.1f;

    // ─── AI Decisions ─────────────────────────────────────────────────────────
    [Header("AI Decisions")]
    [Tooltip("Kick probability at difficulty 0 (game start).")]
    [Range(0f, 1f)]
    [SerializeField] private float kickChanceAtMinDifficulty = 0.3f;

    [Tooltip("Kick probability at difficulty 1 (max).")]
    [Range(0f, 1f)]
    [SerializeField] private float kickChanceAtMaxDifficulty = 0.75f;

    [Tooltip("Hesitate probability at difficulty 0 — gives player breathing room early on.")]
    [Range(0f, 1f)]
    [SerializeField] private float hesitateChanceAtMinDifficulty = 0.3f;

    [Tooltip("Hesitate probability at difficulty 1 — enemies react faster at max difficulty.")]
    [Range(0f, 1f)]
    [SerializeField] private float hesitateChanceAtMaxDifficulty = 0f;

    // ─── Private ──────────────────────────────────────────────────────────────
    private float KickSpeedBonus => isBoss ? 1.5f : 1f;

    private PipeLogic _pipe;
    private Animator _animator;
    private Rigidbody _rb;
    private Camera _mainCamera;

    private Vector2 _committedKickDirection;  // Snapshotted at decision time
    private bool _kickWindowOpen;
    private bool _kickLandedThisSwing;
    private bool _isDead;

    private Coroutine _kickWindowRoutine;
    private Coroutine _invincibilityRoutine;

    private readonly Collider[] _kickHits = new Collider[4];

    [SerializeField] private bool isGrounded;

    private static readonly int IsGroundHash = Animator.StringToHash("isGround");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int IdleHash = Animator.StringToHash("Idle");
    private static readonly int KickRightHash = Animator.StringToHash("kickRight");
    private static readonly int KickLeftHash = Animator.StringToHash("kickLeft");

    #region Unity Lifecycle

    private void Awake()
    {
        PipeLogic[] allPipes = FindObjectsByType<PipeLogic>(FindObjectsInactive.Include);
        foreach (PipeLogic p in allPipes)
        {
            if (!p.isLethalPipe)
            {
                _pipe = p;
                break;
            }
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
        // Poll kick contact every physics tick while window is open —
        // same pattern as player for consistent, reliable hit detection
        if (_kickWindowOpen)
            CheckKickContact();
    }

    private void LateUpdate()
    {
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

    #endregion

    #region AI Decision

    /// <summary>
    /// Called by EnemyTriggerArea after its reaction delay.
    /// Rolls kick / jump / hesitate based on difficulty and Inspector-tuned ranges.
    /// </summary>
    public void DecideAction()
    {
        if (_isDead) return;

        float difficulty = GameManager.instance != null ? GameManager.instance.DifficultyNormalized : 0f;
        float kickChance = Mathf.Lerp(kickChanceAtMinDifficulty, kickChanceAtMaxDifficulty, difficulty);
        float hesitateChance = Mathf.Lerp(hesitateChanceAtMinDifficulty, hesitateChanceAtMaxDifficulty, difficulty);

        kickChance = Mathf.Clamp01(kickChance);
        hesitateChance = Mathf.Clamp01(hesitateChance);

        float roll = Random.value;

        if (roll < kickChance) TryKick();
        else if (roll < 1f - hesitateChance) DoJump();
        // else: hesitate — do nothing this sweep
    }

    #endregion

    #region Movement

    private void DoJump()
    {
        if (!isGrounded) return;

        Vector3 v = _rb.linearVelocity; v.y = 0f; _rb.linearVelocity = v;
        _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        _animator.CrossFade(JumpHash, 0.05f);
    }

    #endregion

    #region Kick — Arrival Timing

    /// <summary>
    /// Calculates pipe arrival time then schedules the animation so
    /// OnKickWindowOpen fires exactly when the pipe tip reaches this enemy.
    /// Falls back to DoJump if there is not enough time to wind up.
    /// </summary>
    private void TryKick()
    {
        if (_pipe == null) return;
        // Snapshot direction NOW before anything can change it
        _committedKickDirection = _pipe.rotationDirection ? Vector2.right : Vector2.left;

        float arrivalTime = CalculatePipeArrivalTime();

        // Not enough time to wind up — jump instead
        float windUpDelay = arrivalTime - kickWindUpDuration;
        if (windUpDelay < minTimeToKick)
        {
            DoJump();
            return;
        }

        StartCoroutine(TimedKickSequence(windUpDelay));
    }

    /// <summary>
    /// Waits windUpDelay seconds then plays the kick animation.
    /// The animation's OnKickWindowOpen event opens the hit window,
    /// which should fire exactly when the pipe arrives.
    /// </summary>
    private IEnumerator TimedKickSequence(float windUpDelay)
    {
        yield return new WaitForSeconds(windUpDelay);

        if (_isDead) yield break;

        // Play animation — OnKickWindowOpen event drives the rest
        _animator.CrossFade(
            _committedKickDirection == Vector2.right ? KickRightHash : KickLeftHash,
            0.02f
        );
    }

    /// <summary>
    /// Calculates how many seconds until the pipe tip reaches this enemy's
    /// angular position, travelling in the current rotation direction.
    ///
    /// Both positions are projected onto the XZ plane relative to the pipe center.
    /// Angular gap / rotation speed (degrees/sec) = arrival time in seconds.
    /// </summary>
    private float CalculatePipeArrivalTime()
    {
        if (_pipe == null || _pipe.pipeTip == null)
        {
            // No tip reference — fall back to a safe fixed estimate
            return kickWindUpDuration + minTimeToKick;
        }

        Vector3 pipeCenter = _pipe.transform.position;

        // Project both positions onto XZ plane relative to pipe center
        Vector3 tipRelative = _pipe.pipeTip.position - pipeCenter; tipRelative.y = 0f;
        Vector3 enemyRelative = transform.position - pipeCenter; enemyRelative.y = 0f;

        if (tipRelative.sqrMagnitude < 0.001f || enemyRelative.sqrMagnitude < 0.001f)
            return kickWindUpDuration + minTimeToKick;

        // Angles in degrees on the XZ plane
        float tipAngle = Mathf.Atan2(tipRelative.z, tipRelative.x) * Mathf.Rad2Deg;
        float enemyAngle = Mathf.Atan2(enemyRelative.z, enemyRelative.x) * Mathf.Rad2Deg;

        // Angular gap in the pipe's travel direction, normalised to [0, 360)
        // rotationDirection true = +Y rotation = counterclockwise on XZ when viewed from above
        float gap;
        if (_pipe.rotationDirection)
            gap = enemyAngle - tipAngle;   // CCW: tip chases enemy forward in angle
        else
            gap = tipAngle - enemyAngle;   // CW: tip chases enemy backward in angle

        gap = ((gap % 360f) + 360f) % 360f;

        // arrivalTime = angular gap / rotation speed (degrees per second)
        float speed = Mathf.Max(_pipe.RuntimeSpeed, 1f);
        return gap / speed;
    }

    #endregion

    #region Kick Window — Animation Event Driven

    /// <summary>
    /// Animation event — fires at wind-up completion (same event as on player clips).
    /// Opens the kick hit window and grants invincibility.
    /// </summary>
    public void OnKickWindowOpen()
    {
        if (_isDead) return;

        _kickLandedThisSwing = false;

        if (_invincibilityRoutine != null) StopCoroutine(_invincibilityRoutine);
        _invincibilityRoutine = StartCoroutine(KickInvincibility());

        if (_kickWindowRoutine != null) StopCoroutine(_kickWindowRoutine);
        _kickWindowRoutine = StartCoroutine(KickWindowRoutine());
    }

    /// <summary>Animation event — optional early close at follow-through end.</summary>
    public void OnKickWindowClose() => CloseKickWindow();

    /// <summary>Legacy stub — kept for clip compatibility.</summary>
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
    /// Uses the committed direction snapshotted at decision time.
    /// </summary>
    private void CheckKickContact()
    {
        if (_kickLandedThisSwing || _pipe == null) return;

        Vector3 origin = kickPoint != null ? kickPoint.position : transform.position;
        int hitCount = Physics.OverlapSphereNonAlloc(origin, kickRange, _kickHits, pipeLayer);
        if (hitCount == 0) return;

        bool landed = _pipe.GetKicked(_committedKickDirection);
        if (!landed) return;

        _kickLandedThisSwing = true;
        CloseKickWindow();

        GameManager.instance?.TriggerHitStop(0.15f, 0.03f);
        Camera.main?.GetComponent<CameraController>()?.TriggerShake(0.06f, 0.15f);
    }

    private IEnumerator KickInvincibility()
    {
        isInvincible = true;
        yield return new WaitForSeconds(kickInvincibilityDuration);
        isInvincible = false;
    }

    #endregion

    #region Health

    public void TakeDamage(int amount)
    {
        if (_isDead || isInvincible) return;

        health -= amount;
        ClampHealth();

        if (health < 1f) Die();
    }

    public void InstantKill()
    {
        _isDead = true;
        SpawnManager.instance.OnEnemyDied(gameObject);
    }

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
        SpawnManager.instance.OnEnemyDied(gameObject);
    }

    #endregion

    #region Heart Display

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

        if (health % 1f != 0f)
        {
            int partialSlot = Mathf.FloorToInt(health);
            if (partialSlot < _heartFills.Length && _heartFills[partialSlot] != null)
                _heartFills[partialSlot].fillAmount = health % 1f;
        }
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

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isBoss ? Color.yellow : Color.red;
        Gizmos.DrawWireSphere(kickPoint != null ? kickPoint.position : transform.position, kickRange);
    }

    #endregion
}