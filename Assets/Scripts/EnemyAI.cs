using System.Collections;
using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    [HideInInspector] public int currentFloor = 0;
    [HideInInspector] public bool isBoss = false;

    public int health = 3;
    public bool isKicking = false;
    public bool isInvincible = false;

    [Header("Jump")]
    public float jumpForce = 18f;

    [Header("Kick Settings")]
    public float kickRange = 1.2f;
    public LayerMask pipeLayer;
    public Transform kickPoint;

    [Header("Invincibility")]
    public float kickInvincibilityDuration = 0.5f;

    // Boss gets a one-time speed multiplier bump per kick
    private float KickSpeedBonus => isBoss ? 1.5f : 1f;

    private PipeLogic _pipe;
    private Animator _animator;
    private Rigidbody _rb;

    [SerializeField] private bool isGrounded;

    // Cached animator hashes
    private static readonly int IsGroundHash = Animator.StringToHash("isGround");

    private void Awake()
    {
        _pipe = FindAnyObjectByType<PipeLogic>();
        _animator = GetComponent<Animator>();
        _rb = GetComponent<Rigidbody>();
    }

    public void DecideAction()
    {
        // hesitateChance decreases with floor; kickChance increases — enemy becomes more aggressive
        float hesitateChance = Mathf.Clamp(0.3f - currentFloor * 0.015f, 0f, 0.3f);
        float kickChance = Mathf.Clamp(0.4f + currentFloor * 0.01f, 0f, 0.75f);
        float roll = Random.value;

        if (roll < kickChance)
            TryKick();
        else if (roll < 1f - hesitateChance)
            DoJump();
        // else: hesitate / do nothing
    }

    private void DoJump()
    {
        _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        _animator.Play("Jump", 0, 0f);
    }

    private void TryKick()
    {
        if (_pipe == null) return;

        Vector3 origin = kickPoint != null ? kickPoint.position : transform.position;
        bool pipeInRange = Physics.CheckSphere(origin, kickRange, pipeLayer);

        if (!pipeInRange)
        {
            // Miss animation — play kick in current pipe direction for visual consistency
            _animator.Play(_pipe.rotationDirection ? "kickRight" : "kickLeft", 0, 0f);
            return;
        }

        Vector2 liveDirection = _pipe.rotationDirection ? Vector2.right : Vector2.left;
        bool landed;

        if (isBoss)
        {
            float original = _pipe.rotationSpeedMultiplier;
            _pipe.rotationSpeedMultiplier = original * KickSpeedBonus;
            landed = _pipe.GetKicked(liveDirection);
            _pipe.rotationSpeedMultiplier = original;
        }
        else
        {
            landed = _pipe.GetKicked(liveDirection);
        }

        _animator.Play(liveDirection == Vector2.right ? "kickRight" : "kickLeft", 0, 0f);

        if (landed)
            StartCoroutine(KickInvincibility());
    }

    // Kept for animation event compatibility — intentionally empty
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
        if (!collision.gameObject.CompareTag("Ground")) return;
        if (!isGrounded) _animator.Play("Idle", 0, 0.1f);

        isGrounded = true;
        _animator.SetBool(IsGroundHash, true);
    }

    private void OnCollisionExit(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Ground")) return;
        isGrounded = false;
        _animator.SetBool(IsGroundHash, false);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isBoss ? Color.yellow : Color.red;
        Gizmos.DrawWireSphere(kickPoint != null ? kickPoint.position : transform.position, kickRange);
    }
}