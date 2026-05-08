using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public class EnemyAI : MonoBehaviour
{
    [HideInInspector] public int currentFloor = 0;
    [HideInInspector] public bool isBoss = false;

    [Header("Health")]
    public int currentHealth = 3;
    public int maxHealth = 3;

    [HideInInspector] public bool isKicking = false;
    [HideInInspector] public bool isInvincible = false;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 18f;

    [Header("Kick Settings")]
    [SerializeField] private float kickRange = 1.2f;
    [SerializeField] private LayerMask pipeLayer;
    [SerializeField] private Transform kickPoint;

    [Header("Invincibility")]
    [SerializeField] private float kickInvincibilityDuration = 0.5f;

    [Header("UI")]
    [SerializeField] private EnemyHealthUI healthUIPrefab;

    private EnemyHealthUI spawnedHealthUI;

    private float KickSpeedBonus => isBoss ? 1.5f : 1f;

    private PipeLogic pipe;
    private Animator animator;
    private Rigidbody rb;

    private Coroutine invincibilityRoutine;

    private Vector2 pendingKickDirection;

    [SerializeField] private bool isGrounded;

    private bool isDead;

    // Animator hashes
    private static readonly int IsGroundHash =
        Animator.StringToHash("isGround");

    private static readonly int JumpHash =
        Animator.StringToHash("Jump");

    private static readonly int IdleHash =
        Animator.StringToHash("Idle");

    private static readonly int KickRightHash =
        Animator.StringToHash("kickRight");

    private static readonly int KickLeftHash =
        Animator.StringToHash("kickLeft");

    private void Awake()
    {
        pipe = FindAnyObjectByType<PipeLogic>();

        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();

        SpawnHealthUI();
    }

    private void OnDestroy()
    {
        if (spawnedHealthUI != null)
        {
            Destroy(spawnedHealthUI.gameObject);
        }
    }

    #region UI

    private void SpawnHealthUI()
    {
        if (healthUIPrefab == null)
            return;

        spawnedHealthUI = Instantiate(healthUIPrefab);

        spawnedHealthUI.Initialize(
            transform,
            maxHealth
        );
    }

    #endregion

    #region AI

    public void DecideAction()
    {
        if (isDead)
            return;

        float difficulty =
            GameManager.instance != null
                ? GameManager.instance.DifficultyNormalized
                : 0f;

        float hesitateChance =
            Mathf.Clamp(
                0.3f - difficulty * 0.3f,
                0f,
                0.3f
            );

        float kickChance =
            Mathf.Clamp(
                0.4f + difficulty * 0.35f,
                0f,
                0.75f
            );

        float roll = Random.value;

        if (roll < kickChance)
        {
            TryKick();
        }
        else if (roll < 1f - hesitateChance)
        {
            DoJump();
        }
    }

    #endregion

    #region Movement

    private void DoJump()
    {
        if (!isGrounded)
            return;

        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;

        rb.linearVelocity = velocity;

        rb.AddForce(
            Vector3.up * jumpForce,
            ForceMode.Impulse
        );

        animator.CrossFade(JumpHash, 0.05f);
    }

    #endregion

    #region Kick

    private void TryKick()
    {
        if (pipe == null)
            return;

        pendingKickDirection =
            pipe.rotationDirection
                ? Vector2.right
                : Vector2.left;

        animator.CrossFade(
            pendingKickDirection == Vector2.right
                ? KickRightHash
                : KickLeftHash,
            0.03f
        );
    }

    public void OnKickWindowOpen()
    {
        OnKickImpact();
    }

    public void OnKickWindowClose()
    {
        OnKickImpact();
    }

    public void OnKickImpact()
    {
        if (isDead)
            return;

        if (pipe == null)
            return;

        Vector3 origin =
            kickPoint != null
                ? kickPoint.position
                : transform.position;

        bool pipeInRange =
            Physics.CheckSphere(
                origin,
                kickRange,
                pipeLayer
            );

        if (!pipeInRange)
            return;

        bool landed;

        if (isBoss)
        {
            float original =
                pipe.rotationSpeedMultiplier;

            pipe.rotationSpeedMultiplier =
                original * KickSpeedBonus;

            landed =
                pipe.GetKicked(pendingKickDirection);

            pipe.rotationSpeedMultiplier = original;
        }
        else
        {
            landed =
                pipe.GetKicked(pendingKickDirection);
        }

        if (!landed)
            return;

        if (invincibilityRoutine != null)
        {
            StopCoroutine(invincibilityRoutine);
        }

        invincibilityRoutine =
            StartCoroutine(KickInvincibility());
    }

    #endregion

    #region Health

    public void TakeDamage(int amount)
    {
        if (isDead)
            return;

        if (isInvincible)
            return;

        currentHealth -= amount;

        spawnedHealthUI?.UpdateHealth(currentHealth);

        if (currentHealth > 0)
            return;

        Die();
    }

    private void Die()
    {
        isDead = true;

        SpawnManager.instance.OnEnemyDied(gameObject);

        Destroy(gameObject);
    }

    private IEnumerator KickInvincibility()
    {
        isInvincible = true;

        yield return new WaitForSeconds(
            kickInvincibilityDuration
        );

        isInvincible = false;
    }

    #endregion

    #region Ground Detection

    private void OnCollisionStay(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Ground"))
            return;

        if (!isGrounded)
        {
            animator.CrossFade(IdleHash, 0.05f);
        }

        isGrounded = true;

        animator.SetBool(IsGroundHash, true);
    }

    private void OnCollisionExit(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Ground"))
            return;

        isGrounded = false;

        animator.SetBool(IsGroundHash, false);
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        Gizmos.color =
            isBoss
                ? Color.yellow
                : Color.red;

        Gizmos.DrawWireSphere(
            kickPoint != null
                ? kickPoint.position
                : transform.position,
            kickRange
        );
    }

    #endregion
}