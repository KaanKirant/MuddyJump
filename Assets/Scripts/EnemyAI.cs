using UnityEngine;
using System.Collections;

public class EnemyAI : MonoBehaviour
{
    [HideInInspector] public int currentFloor = 0;
    [HideInInspector] public bool isBoss = false;

    public int health = 3;
    public bool isKicking = false;
    public bool isInvincible = false;
    public float jumpForce = 18.0f;

    [Header("Kick Settings")]
    public float kickRange = 1.2f;
    public LayerMask pipeLayer;
    public Transform kickPoint;            // Assign a child GameObject at the enemy's foot

    [Header("Invincibility")]
    public float kickInvincibilityDuration = 0.5f;

    private float kickSpeedBonus => isBoss ? 1.5f : 1f;

    private PipeLogic pipe;
    private Animator animator;
    private Rigidbody rb;

    [SerializeField] private bool isGrounded;

    private void Awake()
    {
        pipe = FindAnyObjectByType<PipeLogic>();
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
    }

    public void DecideAction()
    {
        float hesitateChance = Mathf.Clamp(0.3f - currentFloor * 0.015f, 0f, 0.3f);
        float kickChance = Mathf.Clamp(0.4f + currentFloor * 0.01f, 0f, 0.75f);

        float roll = Random.value;

        if (roll < kickChance)
            TryKick();
        else if (roll < 1f - hesitateChance)
            Jump();
    }

    private void Jump()
    {
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        animator.Update(0);
        animator.Play("Jump", 0, 0f);
    }

    private void TryKick()
    {
        if (pipe == null) return;

        // Check range immediately — no waiting for an animation frame
        Vector3 kickOrigin = kickPoint != null ? kickPoint.position : transform.position;
        Collider[] hits = Physics.OverlapSphere(kickOrigin, kickRange, pipeLayer);
        if (hits.Length == 0)
        {
            // Pipe not in range — play the miss animation and do nothing
            bool pipeGoingRight = pipe.rotationDirection;
            animator.Update(0);
            animator.Play(pipeGoingRight ? "kickRight" : "kickLeft", 0, 0f);
            return;
        }

        // Pipe is in range — read live direction and kick immediately
        Vector2 liveDirection = pipe.rotationDirection ? Vector2.right : Vector2.left;
        bool landed;

        if (isBoss)
        {
            float original = pipe.rotationSpeedMultiplier;
            pipe.rotationSpeedMultiplier *= kickSpeedBonus;
            landed = pipe.GetKicked(liveDirection);
            pipe.rotationSpeedMultiplier = original;
        }
        else
        {
            landed = pipe.GetKicked(liveDirection);
        }

        // Play animation for visual feedback regardless of result
        animator.Update(0);
        animator.Play(liveDirection == Vector2.right ? "kickRight" : "kickLeft", 0, 0f);

        // Only grant invincibility if kick actually landed
        if (landed)
            StartCoroutine(KickInvincibility());
    }

    // OnKickImpact is kept but does nothing — animation event can stay
    // on the clips without causing issues
    public void OnKickImpact() { }

    public void TakeDamage(int amount)
    {
        if (isInvincible) return;
        health -= amount;
        if (health <= 0)
            SpawnManager.instance.OnEnemyDied(gameObject);
    }

    private IEnumerator KickInvincibility()
    {
        isInvincible = true;
        yield return new WaitForSeconds(kickInvincibilityDuration);
        isInvincible = false;
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isBoss ? Color.yellow : Color.red;
        Vector3 origin = kickPoint != null ? kickPoint.position : transform.position;
        Gizmos.DrawWireSphere(origin, kickRange);
    }
}