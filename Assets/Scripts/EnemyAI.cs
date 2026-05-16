using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls a single enemy's decision-making, movement, kick logic, and health.
///
/// Decision flow (triggered by EnemyTriggerArea when pipe enters range):
///   DecideAction() → rolls kick/jump/hesitate based on DifficultyNormalized
///   TryKick() → snapshots pipe direction, plays animation, starts KickSequence
///   KickSequence → grants invincibility immediately, resolves pipe impact after kickImpactDelay
///
/// Health display is self-contained — heart containers are instantiated at runtime
/// based on maxTotalHealth and updated via onHealthChangedCallback.
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

    // Fired on every health change — drives UpdateHeartsHUD
    public delegate void OnHealthChangedDelegate();
    public OnHealthChangedDelegate onHealthChangedCallback;

    // ─── Heart Display ────────────────────────────────────────────────────────
    [Header("Heart Display")]
    [Tooltip("Parent transform for instantiated heart containers.")]
    public Transform heartsParent;
    [Tooltip("Same prefab used by HUDController — child 'HeartFill' Image required.")]
    public GameObject heartContainerPrefab;

    private GameObject[] _heartContainers;
    private Image[] _heartFills;

    // ─── State ────────────────────────────────────────────────────────────────
    [HideInInspector] public bool isKicking = false;  // Read by kickBehaviour
    [HideInInspector] public bool isInvincible = false;  // Read by PipeLogic

    // ─── Jump ─────────────────────────────────────────────────────────────────
    [Header("Jump")]
    [SerializeField] private float jumpForce = 18f;

    // ─── Kick ─────────────────────────────────────────────────────────────────
    [Header("Kick Settings")]
    [SerializeField] private float kickRange = 1.2f;
    [SerializeField] private LayerMask pipeLayer;
    [SerializeField] private Transform kickPoint;

    [Tooltip("Seconds after kick starts before pipe impact resolves. " +
             "Match this to the foot-strike frame of your kick animation.")]
    [SerializeField] private float kickImpactDelay = 0.15f;

    [Tooltip("Total invincibility window. Must be >= kickImpactDelay.")]
    [SerializeField] private float kickInvincibilityDuration = 0.6f;

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
        // Billboard hearts toward camera every frame
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

    /// <summary>Called by EnemyTriggerArea after its reaction delay.</summary>
    public void DecideAction()
    {
        if (_isDead) return;

        float difficulty = GameManager.instance != null ? GameManager.instance.DifficultyNormalized : 0f;
        float hesitateChance = Mathf.Clamp(0.3f - difficulty * 0.3f, 0f, 0.3f);
        float kickChance = Mathf.Clamp(0.4f + difficulty * 0.35f, 0f, 0.75f);
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
    /// Step 1 — snapshot pipe direction, play animation.
    /// Pipe is NOT touched here — deferred to KickSequence.
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
    /// Step 2 — invincibility granted immediately on commitment.
    /// Pipe impact resolved after kickImpactDelay seconds.
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

        // Re-read live direction at impact — pipe may have turned since decision
        Vector2 liveDirection = _pipe.rotationDirection ? Vector2.right : Vector2.left;

        if (isBoss)
        {
            float original = _pipe.rotationSpeedMultiplier;
            _pipe.rotationSpeedMultiplier = original * KickSpeedBonus;
            _pipe.GetKicked(liveDirection);
            _pipe.rotationSpeedMultiplier = original;
        }
        else
        {
            _pipe.GetKicked(liveDirection);
        }

        // Lighter hit-stop on successful kick for responsive feedback (timescale 0.15, 0.03s)
        if (GameManager.instance != null) GameManager.instance.TriggerHitStop(0.15f, 0.03f);

        // Camera shake on successful kick impact
        CameraController camera = Camera.main?.GetComponent<CameraController>();
        if (camera != null) camera.TriggerShake(0.06f, 0.15f);

        // Return value ignored — invincibility was already granted unconditionally
    }

    // Legacy animation event stubs — KickSequence drives timing, these are no-ops
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

        // Partial heart for fractional health values
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