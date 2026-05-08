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

    private float KickSpeedBonus => isBoss ? 1.5f : 1f;

    private PipeLogic _pipe;
    private Animator _animator;
    private Rigidbody _rb;

    // Direction decided at kick-start, consumed at OnKickImpact
    private Vector2 _pendingKickDirection;

    [SerializeField] private bool isGrounded;

    private static readonly int IsGroundHash = Animator.StringToHash("isGround");

    private void Awake()
    {
        _pipe = FindAnyObjectByType<PipeLogic>();
        _animator = GetComponent<Animator>();
        _rb = GetComponent<Rigidbody>();
    }

    public void DecideAction()
    {
        float difficulty = GameManager.instance != null ? GameManager.instance.DifficultyNormalized : 0f;
        float hesitateChance = Mathf.Clamp(0.3f - difficulty * 0.3f, 0f, 0.3f);
        float kickChance = Mathf.Clamp(0.4f + difficulty * 0.35f, 0f, 0.75f);
        float roll = Random.value;

        if (roll < kickChance)
            TryKick();
        else if (roll < 1f - hesitateChance)
            DoJump();
        // else: hesitate
    }

    private void DoJump()
    {
        _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        _animator.Play("Jump", 0, 0f);
    }

    // ── Step 1: decide direction, play animation — pipe NOT touched yet ───────
    private void TryKick()
    {
        if (_pipe == null) return;

        // Snapshot the pipe direction NOW so the animation visually matches intent.
        // Pipe interaction is deferred to OnKickImpact so the effect lands at the
        // same frame the foot visually strikes — identical to the player flow.
        _pendingKickDirection = _pipe.rotationDirection ? Vector2.right : Vector2.left;
        _animator.Play(_pendingKickDirection == Vector2.right ? "kickRight" : "kickLeft", 0, 0f);
    }

    public void OnKickWindowOpen() => OnKickImpact();

    public void OnKickWindowClose() => OnKickImpact();

    // ── Step 2: animation event fires at foot-strike frame ────────────────────
    public void OnKickImpact()
    {
        if (_pipe == null) return;

        // Range check at impact frame — enemy must actually be close enough
        Vector3 origin = kickPoint != null ? kickPoint.position : transform.position;
        bool pipeInRange = Physics.CheckSphere(origin, kickRange, pipeLayer);
        if (!pipeInRange) return;

        bool landed;

        if (isBoss)
        {
            float original = _pipe.rotationSpeedMultiplier;
            _pipe.rotationSpeedMultiplier = original * KickSpeedBonus;
            landed = _pipe.GetKicked(_pendingKickDirection);
            _pipe.rotationSpeedMultiplier = original;
        }
        else
        {
            landed = _pipe.GetKicked(_pendingKickDirection);
        }

        if (landed)
            StartCoroutine(KickInvincibility());
    }

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