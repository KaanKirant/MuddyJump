using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Player Stats")]
    [SerializeField] private float health = 3f;
    [SerializeField] private float actionCooldown = 0.2f;

    [Header("Physics Settings")]
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private float slamForce = 18f;
    [SerializeField] private float fallGravity = 30f;

    [Header("Kick Settings")]
    [SerializeField] private float kickRange = 1.5f;
    [SerializeField] private float kickHeightOffset = 0.5f;
    [SerializeField] private LayerMask pipeLayer;
    [SerializeField] private float kickDuration = 0.25f;

    [Header("Invincibility")]
    [SerializeField] private float kickInvincibilityDuration = 0.4f;

    [Header("Debug")]
    [SerializeField] private bool isGrounded;

    public bool IsKicking { get; private set; }
    public bool IsInvincible { get; private set; }

    private Rigidbody rb;
    private Animator animator;
    private PipeLogic pipe;

    private Vector2 currentKickDirection;

    private float lastJumpTime;
    private float lastKickTime;

    private Coroutine invincibilityRoutine;
    private Coroutine kickRoutine;

    private readonly Collider[] kickHits = new Collider[4];

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
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        rb.constraints = RigidbodyConstraints.FreezeRotation
                       | RigidbodyConstraints.FreezePositionX
                       | RigidbodyConstraints.FreezePositionZ;

        pipe = FindAnyObjectByType<PipeLogic>();
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
        if (!isGrounded)
        {
            rb.AddForce(Vector3.down * fallGravity, ForceMode.Acceleration);
        }
    }

    #endregion

    #region Input

    private void OnSwipe(Vector2 direction)
    {
        if (direction.y > 0.5f)
        {
            TryJump();
            return;
        }

        if (direction.y < -0.5f)
        {
            TryFastFall();
            return;
        }

        if (Mathf.Abs(direction.x) > 0.5f)
        {
            TryKick(direction);
        }
    }

    #endregion

    #region Actions

    private void TryJump()
    {
        if (!isGrounded)
            return;

        if (Time.time < lastJumpTime + actionCooldown)
            return;

        Jump();
    }

    private void Jump()
    {
        lastJumpTime = Time.time;

        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        rb.linearVelocity = velocity;

        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

        animator.CrossFade(JumpHash, 0.05f);
    }

    private void TryFastFall()
    {
        if (isGrounded)
            return;

        FastFall();
    }

    private void FastFall()
    {
        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        rb.linearVelocity = velocity;

        rb.angularVelocity = Vector3.zero;

        rb.AddForce(Vector3.down * slamForce, ForceMode.Impulse);

        animator.CrossFade(RollHash, 0.05f);
    }

    private void TryKick(Vector2 direction)
    {
        if (!isGrounded)
            return;

        if (Time.time < lastKickTime + actionCooldown)
            return;

        Kick(direction);
    }

    private void Kick(Vector2 direction)
    {
        lastKickTime = Time.time;

        currentKickDirection = direction;

        if (kickRoutine != null)
            StopCoroutine(kickRoutine);

        kickRoutine = StartCoroutine(KickStateRoutine());

        animator.CrossFade(
            direction.x > 0f ? KickRightHash : KickLeftHash,
            0.03f
        );
    }

    public void OnKickImpact()
    {
        Vector3 kickOrigin =
            transform.position - new Vector3(0f, kickHeightOffset, 0f);

        int hitCount = Physics.OverlapSphereNonAlloc(
            kickOrigin,
            kickRange,
            kickHits,
            pipeLayer
        );

        if (hitCount <= 0 || pipe == null)
            return;

        bool validDirection =
            (currentKickDirection.x > 0f && pipe.rotationDirection) ||
            (currentKickDirection.x < 0f && !pipe.rotationDirection);

        if (!validDirection)
            return;

        bool landed = pipe.GetKicked(currentKickDirection);

        if (!landed)
            return;

        GameManager.instance.AddScore(1);

        if (invincibilityRoutine != null)
            StopCoroutine(invincibilityRoutine);

        invincibilityRoutine = StartCoroutine(KickInvincibility());
    }

    #endregion

    #region Kick State

    private IEnumerator KickStateRoutine()
    {
        IsKicking = true;

        yield return new WaitForSeconds(kickDuration);

        IsKicking = false;
    }

    #endregion

    #region Combat / Damage

    public void TakeDamage(float amount)
    {
        if (IsInvincible)
            return;

        Vector3 velocity = rb.linearVelocity;
        velocity.x = 0f;
        velocity.z = 0f;

        rb.linearVelocity = velocity;
        rb.angularVelocity = Vector3.zero;

        health -= amount;

        if (health <= 0f)
        {
            GameManager.instance.EndGame();
        }
    }

    private IEnumerator KickInvincibility()
    {
        IsInvincible = true;

        yield return new WaitForSeconds(kickInvincibilityDuration);

        IsInvincible = false;
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

    #region Public API

    public void SetPipeLogic(PipeLogic targetPipe)
    {
        pipe = targetPipe;
    }

    public void SetKickState(bool value)
    {
        IsKicking = value;
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;

        Vector3 origin = transform.position - new Vector3(0f, kickHeightOffset, 0f);

        Gizmos.DrawWireSphere(origin, kickRange);
    }

    #endregion
}