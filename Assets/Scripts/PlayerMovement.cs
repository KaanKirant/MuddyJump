using System.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public GameObject pipeObject;
    public float health = 3f;
    public float actionCooldown = 0.2f; // Lowered for snappier feel
    public bool isKicking = false;

    [Header("Physics Settings")]
    public float jumpForce = 12f;    // Increased for punchier jump
    public float slamForce = 18f;    // High slam force for instant floor snap
    public float fallGravity = 30f;  // Stronger pull when falling

    private float lastJumpTime;
    private float lastKickTime;

    private Rigidbody rb;
    private Animator animator;

    [SerializeField] private bool isGrounded;

    private void Awake()
    {
        // Direct subscription is cleaner
        SwipeDetection.instance.swipePerformed += Swipe;

        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        // Ensure Rigidbody constraints are set for a runner
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    private void FixedUpdate()
    {
        // Apply heavy gravity only when in air to avoid jitter on ground
        if (!isGrounded)
        {
            rb.AddForce(Vector3.down * fallGravity, ForceMode.Acceleration);
        }
    }

    private void Swipe(Vector2 direction)
    {
        // JUMP (Up)
        if (direction.y > 0.5f && isGrounded)
        {
            if (Time.time > lastJumpTime + actionCooldown)
            {
                Jump();
            }
        }
        // FAST FALL / ROLL (Down) - Note the negative 0.5f
        else if (direction.y < -0.5f && !isGrounded)
        {
            FastFall();
        }
        // KICK (Sides)
        else if (Mathf.Abs(direction.x) > 0.5f)
        {
            if (Time.time > lastKickTime + actionCooldown)
            {
                Kick(direction);
            }
        }
    }

    private void Jump()
    {
        // Kill existing velocity for instant launch
        rb.linearVelocity = Vector3.zero;
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

        lastJumpTime = Time.time;

        // Play jump instantly (bypass transition lag)
        animator.Update(0);
        animator.Play("Jump", 0, 0f);
    }

    private void FastFall()
    {
        rb.linearVelocity = Vector3.zero;
        rb.AddForce(Vector3.down * slamForce, ForceMode.Impulse);

        // Force the animation
        animator.Update(0);
        animator.Play("Roll", 0, 0f);

        // DISABLE rotation on the Rigidbody completely 
        // to prevent the animation from 'tipping' the physics capsule
        rb.angularVelocity = Vector3.zero;
    }

    private void Kick(Vector2 dir)
    {
        pipeObject.GetComponent<PipeLogic>().GetKicked(dir);

        // Swipe Right
        if (dir.x > 0.5f)
        {
            animator.Play("kickRight", 0, 0f);
            animator.SetTrigger("kickRight"); // Set trigger for right kick
        }
        // Swipe Left
        else if (dir.x < -0.5f)
        { 
            animator.Play("kickLeft", 0, 0f);
            animator.SetTrigger("kickLeft"); // Set trigger for right kick
        }

        lastKickTime = Time.time;
    }

    // --- Grounding Logic ---
    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            if (!isGrounded)
            {
                animator.Play("Idle", 0, 0.1f); // 0.1f adds a tiny blend so it's not a hard pop
            }
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

    // Clean up event on destroy
    private void OnDestroy()
    {
        SwipeDetection.instance.swipePerformed -= Swipe;
    }
}