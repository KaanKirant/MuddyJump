using System.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public GameObject pipeObject;
    public float health = 3f;
    public float actionCooldown = 0.2f;
    public bool isKicking = false;

    [Header("Physics Settings")]
    public float jumpForce = 12f;
    public float slamForce = 18f;
    public float fallGravity = 30f;

    private float lastJumpTime;
    private float lastKickTime;

    private Rigidbody rb;
    private Animator animator;

    [SerializeField] private bool isGrounded;

    private void Awake()
    {
        SwipeDetection.instance.swipePerformed += OnSwipe;
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
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
        else if (Mathf.Abs(direction.x) > 0.5f)
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
        if(!isGrounded) return; // Prevent kicking while in the air
        pipeObject.GetComponent<PipeLogic>().GetKicked(dir);

        // FIX: animator.Play and SetTrigger were both called redundantly.
        // Using Play() directly is consistent with Jump() and FastFall().
        if (dir.x > 0.5f)
        {
            animator.Update(0);
            animator.Play("kickRight", 0, 0f);
            //SetTrigger to change state immediately
        }
        else if (dir.x < -0.5f)
        {
            animator.Update(0);
            animator.Play("kickLeft", 0, 0f);
            //SetTrigger to change state immediately
        }

        lastKickTime = Time.time;
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
}