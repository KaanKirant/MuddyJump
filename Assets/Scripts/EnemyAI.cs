using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls a single enemy's decision-making, movement, kick logic, and health.
///
/// Decision flow (triggered by EnemyTriggerArea when pipe enters range):
///   DecideAction() → rolls kick/jump/hesitate based on difficulty and tunable ranges
///   TryKick() → snapshots pipe direction, plays animation, starts KickSequence
///   KickSequence → grants invincibility immediately, resolves pipe impact after kickImpactDelay
///
/// All AI decision probabilities are exposed in the Inspector under "AI Decisions"
/// so behaviour can be tuned without touching code.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public class EnemyAI : MonoBehaviour
{
    // ─── Identity ─────────────────────────────────────────────────────────────
    [HideInInspector] public bool isBoss = false;   // Set by SpawnManager after instantiation

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

    // ─── Kick ─────────────────────────────────────────────────────────────────
    [Header("Kick Settings")]
    [SerializeField] private float kickRange = 1.2f;
    [SerializeField] private LayerMask pipeLayer;
    [SerializeField] private Transform kickPoint;

    [Tooltip("Seconds after kick animation starts before pipe impact is resolved. " +
             "Match to the foot-strike frame of your kick animation.")]
    [SerializeField] private float kickImpactDelay = 0.15f;

    [Tooltip("Total invincibility window from kick start. Must be >= kickImpactDelay.")]
    [SerializeField] private float kickInvincibilityDuration = 0.6f;

    // ─── AI Decisions ─────────────────────────────────────────────────────────
    [Header("AI Decisions")]
    [Tooltip("Kick probability at difficulty 0 (game start). 0 = never kicks, 1 = always kicks.")]
    [Range(0f, 1f)]
    [SerializeField] private float kickChanceAtMinDifficulty = 0.3f;

    [Tooltip("Kick probability at difficulty 1 (max). Should be higher than min.")]
    [Range(0f, 1f)]
    [SerializeField] private float kickChanceAtMaxDifficulty = 0.75f;

    [Tooltip("Hesitate (do nothing) probability at difficulty 0. Gives the player breathing room early game.")]
    [Range(0f, 1f)]
    [SerializeField] private float hesitateChanceAtMinDifficulty = 0.3f;

    [Tooltip("Hesitate probability at difficulty 1. Should be lower than min — enemies react faster at high difficulty.")]
    [Range(0f, 1f)]
    [SerializeField] private float hesitateChanceAtMaxDifficulty = 0f;

    // ─── Private ──────────────────────────────────────────────────────────────
    private float KickSpeedBonus => isBoss ? 1.5f : 1f;

    private PipeLogic _pipe;
    private Animator _animator;
    private Rigidbody _rb;
    private Camera _mainCamera;

    private Vector2 _pendingKickDirection;
    private bool _isDead;

    [SerializeField] private bool isGrounded;

    private static readonly int IsGroundHash = Animator.StringToHash("isGround");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int IdleHash = Animator.StringToHash("Idle");
    private static readonly int KickRightHash = Animator.StringToHash("kickRight");
    private static readonly int KickLeftHash = Animator.StringToHash("kickLeft");

    #region Unity Lifecycle

    private void Awake()
    {
        _pipe = FindAnyObjectByType<PipeLogic>();
        _animator = GetComponent<Animator>();
        _rb = GetComponent<Rigidbody>();
        _mainCamera = Camera.main;

        InstantiateHeartContainers();
        onHealthChangedCallback += UpdateHeartsHUD;
        UpdateHeartsHUD();
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
    /// Rolls kick / jump / hesitate based on current difficulty and Inspector-tuned ranges.
    /// </summary>
    public void DecideAction()
    {
        if (_isDead) return;

        float difficulty = GameManager.instance != null ? GameManager.instance.DifficultyNormalized : 0f;

        // Lerp both chances across the difficulty range — fully tunable in Inspector
        float kickChance = Mathf.Lerp(kickChanceAtMinDifficulty, kickChanceAtMaxDifficulty, difficulty);
        float hesitateChance = Mathf.Lerp(hesitateChanceAtMinDifficulty, hesitateChanceAtMaxDifficulty, difficulty);

        // Clamp so kick + hesitate never exceed 1 (remaining probability goes to jump)
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

    #region Kick

    /// <summary>
    /// Step 1 — snapshot pipe direction and play animation.
    /// Pipe contact is deferred to KickSequence.
    /// </summary>
    private void TryKick()
    {
        if (_pipe == null) return;

        _pendingKickDirection = _pipe.rotationDirection ? Vector2.right : Vector2.left;

        _animator.CrossFade(
            _pendingKickDirection == Vector2.right ? KickRightHash : KickLeftHash,
            0.02f
        );

        StartCoroutine(KickSequence());
    }

    /// <summary>
    /// Step 2 — invincibility granted immediately on kick commitment.
    /// Pipe impact resolved after kickImpactDelay to match the animation.
    /// </summary>
    private IEnumerator KickSequence()
    {
        isInvincible = true;
        isKicking = true;

        yield return new WaitForSeconds(kickImpactDelay);

        if (!_isDead)
            ResolveKickImpact();

        float remaining = Mathf.Max(0f, kickInvincibilityDuration - kickImpactDelay);
        yield return new WaitForSeconds(remaining);

        isInvincible = false;
        isKicking = false;
    }

    private void ResolveKickImpact()
    {
        if (_pipe == null) return;

        Vector3 origin = kickPoint != null ? kickPoint.position : transform.position;
        bool pipeInRange = Physics.CheckSphere(origin, kickRange, pipeLayer);
        if (!pipeInRange) return;

        // Re-read live direction at impact — pipe may have turned since the decision
        Vector2 liveDirection = _pipe.rotationDirection ? Vector2.right : Vector2.left;

        if (isBoss)
        {
            // Boss temporarily amplifies kick multiplier for a harder hit
            float original = _pipe.kickSpeedMultiplier;
            _pipe.kickSpeedMultiplier = original * KickSpeedBonus;
            _pipe.GetKicked(liveDirection);
            _pipe.kickSpeedMultiplier = original;
        }
        else
        {
            _pipe.GetKicked(liveDirection);
        }

        GameManager.instance?.TriggerHitStop(0.15f, 0.03f);

        CameraController cam = Camera.main?.GetComponent<CameraController>();
        cam?.TriggerShake(0.06f, 0.15f);
    }

    // Legacy animation event stubs — KickSequence drives timing
    public void OnKickImpact() { }
    public void OnKickWindowOpen() { }
    public void OnKickWindowClose() { }

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