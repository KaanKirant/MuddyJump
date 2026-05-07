using System.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float health = 3f;
    public float actionCooldown = 0.2f;
    public bool isKicking = false;
    public bool isInvincible = false;

    [Header("Physics Settings")]
    public float jumpForce = 12f;
    public float slamForce = 18f;
    public float fallGravity = 30f;

    [Header("Kick Settings")]
    public float kickRange = 1.5f;
    public float kickHeightOffset = 0.5f;   // How far below center to check (foot level)
    public LayerMask pipeLayer;

    [Header("Invincibility")]
    public float kickInvincibilityDuration = 0.4f;

    private float lastJumpTime;
    private float lastKickTime;

    private Rigidbody rb;
    private Animator animator;
    private PipeLogic pipe;

    [SerializeField] private bool isGrounded;

    private void Awake()
    {
        SwipeDetection.instance.swipePerformed += OnSwipe;
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        rb.constraints = RigidbodyConstraints.FreezeRotation
                       | RigidbodyConstraints.FreezePositionX
                       | RigidbodyConstraints.FreezePositionZ;
        pipe = FindAnyObjectByType<PipeLogic>();
    }

    private void FixedUpdate()
    {
        if (!isGrounded)
            rb.AddForce(Vector3.down * fallGravity, ForceMode.Acceleration);
    }

    private void OnSwipe(Vector2 direction)
    {
        if (direction.y > 0.5f && isGrounded)
        {
            if (Time.time > lastJumpTime + actionCooldown)
                Jump();
        }
        else if (direction.y < -0.5f && !isGrounded)
        {
            FastFall();
        }
        else if (Mathf.Abs(direction.x) > 0.5f && isGrounded)
        {
            if (Time.time > lastKickTime + actionCooldown)
                Kick(direction);
        }
    }

    private void Jump()
    {
        rb.linearVelocity = Vector3.zero;
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        lastJumpTime = Time.time;
        animator.Update(0);
        animator.Play("Jump", 0, 0f);
    }

    private void FastFall()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.AddForce(Vector3.down * slamForce, ForceMode.Impulse);
        animator.Update(0);
        animator.Play("Roll", 0, 0f);
    }

    private void Kick(Vector2 dir)
    {
        lastKickTime = Time.time;

        // Check at foot level — offset below center
        Vector3 kickOrigin = transform.position - new Vector3(0f, kickHeightOffset, 0f);
        Collider[] hits = Physics.OverlapSphere(kickOrigin, kickRange, pipeLayer);

        bool landed = false;
        if (hits.Length > 0 && pipe != null)
        {
            // Directional check: swipe must match pipe's current rotation direction
            // Right swipe only works if pipe is going clockwise (rotationDirection = true)
            // Left swipe only works if pipe is going counter-clockwise
            bool validDirection = (dir.x > 0f && pipe.rotationDirection) ||
                                  (dir.x < 0f && !pipe.rotationDirection);

            if (validDirection)
            {
                landed = pipe.GetKicked(dir);
                if (landed)
                {
                    GameManager.instance.AddScore(1);
                    StartCoroutine(KickInvincibility());
                }
            }
        }

        // Always play animation — swipe should always feel responsive
        animator.Update(0);
        animator.Play(dir.x > 0f ? "kickRight" : "kickLeft", 0, 0f);
    }

    // Empty — kept so animation events on clips don't throw errors
    public void OnKickImpact() { }

    private IEnumerator KickInvincibility()
    {
        isInvincible = true;
        yield return new WaitForSeconds(kickInvincibilityDuration);
        isInvincible = false;
    }

    public void TakeDamage(float amount)
    {
        if (isInvincible) return;

        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        rb.angularVelocity = Vector3.zero;

        health -= amount;
        if (health <= 0)
            GameManager.instance.EndGame();
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            if (!isGrounded)
                animator.Play("Idle", 0, 0.1f);

            isGrounded = true;
            animator.SetBool("isGround", true);
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = false;
            animator.SetBool("isGround", false);
        }
    }

    private void OnDestroy()
    {
        SwipeDetection.instance.swipePerformed -= OnSwipe;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 origin = transform.position - new Vector3(0f, kickHeightOffset, 0f);
        Gizmos.DrawWireSphere(origin, kickRange);
    }
}