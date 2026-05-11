using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;

/// <summary>
/// Controls a single enemy's decision-making, movement, kick logic, and health.
///
/// Decision flow (triggered by EnemyTriggerArea when pipe enters range):
///   DecideAction() → rolls kick/jump/hesitate based on DifficultyNormalized
///   TryKick() → plays animation, starts KickSequence coroutine
///   KickSequence → grants invincibility immediately, resolves impact after kickImpactDelay
///
/// Invincibility is granted at kick commitment — not conditional on pipe cooldown.
/// This ensures enemies are never punished for kicking correctly.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public class EnemyAI : MonoBehaviour
{
    public delegate void OnHealthChangedDelegate();
    public OnHealthChangedDelegate onHealthChangedCallback;
    // ─── Identity ─────────────────────────────────────────────────────────────
    // Set by SpawnManager after instantiation
    [HideInInspector] public bool isBoss = false;

    // ─── Health ───────────────────────────────────────────────────────────────
    [Header("Health")]
    [SerializeField]
    private float health;
    [SerializeField]
    private float maxHealth;
    [SerializeField]
    private float maxTotalHealth;
    public float Health { get { return health; } set { health = value; } }
    public float MaxHealth { get { return maxHealth; } set { maxHealth = value; } }
    public float MaxTotalHealth { get { return maxTotalHealth; } }

    private GameObject[] heartContainers;
    private Image[] heartFills;

    public Transform heartsParent;
    public GameObject heartContainerPrefab;

    // ─── State (read by PipeLogic for invincibility checks) ───────────────────
    [HideInInspector] public bool isKicking = false;
    [HideInInspector] public bool isInvincible = false;

    // ─── Jump ─────────────────────────────────────────────────────────────────
    [Header("Jump")]
    [SerializeField] private float jumpForce = 18f;

    // ─── Kick ─────────────────────────────────────────────────────────────────
    [Header("Kick Settings")]
    [SerializeField] private float kickRange = 1.2f;
    [SerializeField] private LayerMask pipeLayer;
    [SerializeField] private Transform kickPoint;   // Child transform at foot position

    [Tooltip("Seconds after kick starts before pipe impact is resolved. " +
             "Set this to match the foot-strike frame of your kick animation.")]
    [SerializeField] private float kickImpactDelay = 0.15f;

    [Tooltip("Total invincibility window. Should cover the full kick animation length. " +
             "Must be >= kickImpactDelay.")]
    [SerializeField] private float kickInvincibilityDuration = 0.6f;

    // ─── UI ───────────────────────────────────────────────────────────────────
    [Header("UI")]
    [SerializeField] private EnemyHealthUI healthUIPrefab;  // World-space health bar prefab

    // ─── Private ──────────────────────────────────────────────────────────────
    // Boss kicks temporarily amplify the speed multiplier for a stronger hit
    private float KickSpeedBonus => isBoss ? 1.5f : 1f;

    private PipeLogic _pipe;
    private Animator _animator;
    private Rigidbody _rb;
    private Camera _mainCamera;

    private EnemyHealthUI _spawnedHealthUI;
    private Vector2 _pendingKickDirection;  // Snapshotted at kick start, used at impact
    private bool _isDead;

    [SerializeField] private bool isGrounded;

    // Animator hashes — computed once, never allocate strings at runtime
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

        // Should I use lists? Maybe :)
        heartContainers = new GameObject[(int)PlayerStats.Instance.MaxTotalHealth];
        heartFills = new Image[(int)PlayerStats.Instance.MaxTotalHealth];

        onHealthChangedCallback += UpdateHeartsHUD;
        InstantiateHeartContainers();
        UpdateHeartsHUD();

        //SpawnHealthUI();
    }
    private void LateUpdate()
    {
        // Billboard toward camera
        if (_mainCamera != null)
        {
            heartsParent.LookAt(heartsParent.transform.position + _mainCamera.transform.rotation * Vector3.forward, _mainCamera.transform.rotation * Vector3.up);
            
            /*heartsParent.rotation =
                Quaternion.LookRotation(
                    heartsParent.position -
                    _mainCamera.transform.position
                );*/
        }
    }

    private void OnDestroy()
    {
        // Health UI is a separate GameObject — must be cleaned up manually
        if (_spawnedHealthUI != null)
            Destroy(_spawnedHealthUI.gameObject);
    }

    #endregion

    #region UI

    /*
    private void SpawnHealthUI()
    {
        if (healthUIPrefab == null) return;
        _spawnedHealthUI = Instantiate(healthUIPrefab);
        _spawnedHealthUI.Initialize(transform, maxHealth);
    }*/

    #endregion

    #region AI Decision

    /// <summary>
    /// Called by EnemyTriggerArea after its reaction delay.
    /// Rolls a decision based on current difficulty.
    /// </summary>
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

        // Zero vertical velocity for consistent jump height (same pattern as PlayerMovement)
        Vector3 v = _rb.linearVelocity; v.y = 0f; _rb.linearVelocity = v;

        _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        _animator.CrossFade(JumpHash, 0.05f);
    }

    #endregion

    #region Kick

    /// <summary>
    /// Step 1 — snapshot pipe direction and start animation.
    /// Pipe is NOT touched here — impact is deferred to KickSequence.
    /// </summary>
    private void TryKick()
    {
        if (_pipe == null) return;

        // Snapshot current direction for animation choice and impact resolution
        _pendingKickDirection = _pipe.rotationDirection ? Vector2.right : Vector2.left;

        _animator.CrossFade(
            _pendingKickDirection == Vector2.right ? KickRightHash : KickLeftHash,
            0.03f
        );

        StartCoroutine(KickSequence());
    }

    /// <summary>
    /// Step 2 — grants invincibility immediately, resolves pipe impact after kickImpactDelay.
    /// Invincibility is unconditional — the enemy committed to the kick regardless of
    /// whether the pipe cooldown blocks the speed change.
    /// </summary>
    private IEnumerator KickSequence()
    {
        isInvincible = true;
        isKicking = true;

        yield return new WaitForSeconds(kickImpactDelay);

        if (!_isDead)
            ResolveKickImpact();

        // Hold invincibility for the remainder of the animation
        float remaining = Mathf.Max(0f, kickInvincibilityDuration - kickImpactDelay);
        yield return new WaitForSeconds(remaining);

        isInvincible = false;
        isKicking = false;
    }

    /// <summary>
    /// Checks range and calls GetKicked at the visual impact frame.
    /// Enemy still keeps their invincibility even if the pipe is on cooldown.
    /// </summary>
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
            // Boss temporarily amplifies the speed multiplier for a harder kick
            float original = _pipe.rotationSpeedMultiplier;
            _pipe.rotationSpeedMultiplier = original * KickSpeedBonus;
            _pipe.GetKicked(liveDirection);
            _pipe.rotationSpeedMultiplier = original;
        }
        else
        {
            _pipe.GetKicked(liveDirection);
        }
        // Return value ignored — invincibility was already granted at kick start
    }

    // Kept for any legacy animation event on clips — no-op, KickSequence drives timing now
    public void OnKickImpact() { }
    public void OnKickWindowOpen() { }
    public void OnKickWindowClose() { }

    #endregion

    #region Health

    /// <summary>Called by PipeLogic.OnCollisionEnter when pipe hits enemy without invincibility.</summary>
    public void TakeDamage(int amount)
    {
        if (_isDead || isInvincible) return;

        health -= amount;
        ClampHealth();

        if (health < 1)
        {
            Die();
        }
    }

    public void Heal(float health)
    {
        this.health += health;
        ClampHealth();
    }

    void ClampHealth()
    {
        health = Mathf.Clamp(health, 0, maxHealth);

        if (onHealthChangedCallback != null)
            onHealthChangedCallback.Invoke();
    }

    public void UpdateHeartsHUD()
    {
        SetHeartContainers();
        SetFilledHearts();
    }

    void SetHeartContainers()
    {
        for (int i = 0; i < heartContainers.Length; i++)
        {
            if (i < maxHealth)
            {
                heartContainers[i].SetActive(true);
            }
            else
            {
                heartContainers[i].SetActive(false);
            }
        }
    }
    void SetFilledHearts()
    {
        for (int i = 0; i < heartFills.Length; i++)
        {
            if (i < health)
            {
                heartFills[i].fillAmount = 1;
            }
            else
            {
                heartFills[i].fillAmount = 0;
            }
        }

        if (health % 1 != 0)
        {
            int lastPos = Mathf.FloorToInt(health);
            heartFills[lastPos].fillAmount = health % 1;
        }
    }

    void InstantiateHeartContainers()
    {
        for (int i = 0; i < maxTotalHealth; i++)
        {
            GameObject temp = Instantiate(heartContainerPrefab);
            temp.transform.SetParent(heartsParent, false);
            heartContainers[i] = temp;
            heartFills[i] = temp.transform.Find("HeartFill").GetComponent<Image>();
        }
    }

    private void Die()
    {
        _isDead = true;
        // SpawnManager handles Destroy — don't call it here and in SpawnManager both
        SpawnManager.instance.OnEnemyDied(gameObject);
    }

    private IEnumerator KickInvincibility()
    {
        isInvincible = true;
        yield return new WaitForSeconds(kickInvincibilityDuration);
        isInvincible = false;
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