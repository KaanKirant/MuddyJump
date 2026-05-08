using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Player Stats")]
    [SerializeField] private int health = 3;
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

    private Rigidbody _rb;
    private Animator _animator;
    private PipeLogic _pipe;

    private Vector2 _currentKickDirection;
    private float _lastJumpTime;
    private float _lastKickTime;

    private Coroutine _invincibilityRoutine;
    private Coroutine _kickRoutine;

    private readonly Collider[] _kickHits = new Collider[4];

    // Animator hashes — computed once
    private static readonly int IsGroundHash = Animator.StringToHash("isGround");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int RollHash = Animator.StringToHash("Roll");
    private static readonly int KickRightHash = Animator.StringToHash("kickRight");
    private static readonly int KickLeftHash = Animator.StringToHash("kickLeft");
    private static readonly int IdleHash = Animator.StringToHash("Idle");

    #region Unity Lifecycle

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();

        _rb.constraints = RigidbodyConstraints.FreezeRotation
                        | RigidbodyConstraints.FreezePositionX
                        | RigidbodyConstraints.FreezePositionZ;

        _pipe = FindAnyObjectByType<PipeLogic>();
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
        // Extra gravity only while airborne — avoids fighting physics on ground
        if (!isGrounded)
            _rb.AddForce(Vector3.down * fallGravity, ForceMode.Acceleration);
    }

    #endregion

    #region Input

    private void OnSwipe(Vector2 direction)
    {
        if (direction.y > 0.5f) { TryJump(); return; }
        if (direction.y < -0.5f) { TryFastFall(); return; }
        if (Mathf.Abs(direction.x) > 0.5f) TryKick(direction);
    }

    #endregion

    #region Actions

    private void TryJump()
    {
        if (!isGrounded || Time.time < _lastJumpTime + actionCooldown) return;
        DoJump();
    }

    private void DoJump()
    {
        _lastJumpTime = Time.time;

        // Zero out vertical velocity for consistent jump height
        Vector3 v = _rb.linearVelocity; v.y = 0f; _rb.linearVelocity = v;
        _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        _animator.CrossFade(JumpHash, 0.05f);
    }

    private void TryFastFall()
    {
        if (isGrounded) return;
        DoFastFall();
    }

    private void DoFastFall()
    {
        // Zero vertical and angular velocity so slam is deterministic
        Vector3 v = _rb.linearVelocity; v.y = 0f; _rb.linearVelocity = v;
        _rb.angularVelocity = Vector3.zero;

        _rb.AddForce(Vector3.down * slamForce, ForceMode.Impulse);
        _animator.CrossFade(RollHash, 0.05f);
    }

    private void TryKick(Vector2 direction)
    {
        if (!isGrounded || Time.time < _lastKickTime + actionCooldown) return;
        DoKick(direction);
    }

    private void DoKick(Vector2 direction)
    {
        _lastKickTime = Time.time;
        _currentKickDirection = direction;

        if (_kickRoutine != null) StopCoroutine(_kickRoutine);
        _kickRoutine = StartCoroutine(KickStateRoutine());

        _animator.CrossFade(direction.x > 0f ? KickRightHash : KickLeftHash, 0.03f);
    }

    // Called from animation event
    public void OnKickImpact()
    {
        if (_pipe == null) return;

        Vector3 kickOrigin = transform.position - new Vector3(0f, kickHeightOffset, 0f);
        int hitCount = Physics.OverlapSphereNonAlloc(kickOrigin, kickRange, _kickHits, pipeLayer);
        if (hitCount == 0) return;

        bool validDirection = (_currentKickDirection.x > 0f && _pipe.rotationDirection) ||
                              (_currentKickDirection.x < 0f && !_pipe.rotationDirection);
        if (!validDirection) return;

        bool landed = _pipe.GetKicked(_currentKickDirection);
        if (!landed) return;

        GameManager.instance.AddScore(1);

        if (_invincibilityRoutine != null) StopCoroutine(_invincibilityRoutine);
        _invincibilityRoutine = StartCoroutine(KickInvincibility());
    }

    #endregion

    #region Coroutines

    private IEnumerator KickStateRoutine()
    {
        IsKicking = true;
        yield return new WaitForSeconds(kickDuration);
        IsKicking = false;
    }

    private IEnumerator KickInvincibility()
    {
        IsInvincible = true;
        yield return new WaitForSeconds(kickInvincibilityDuration);
        IsInvincible = false;
    }

    #endregion

    #region Combat

    public void TakeDamage(int amount)
    {
        if (IsInvincible) return;

        // Kill horizontal drift on hit
        Vector3 v = _rb.linearVelocity; v.x = 0f; v.z = 0f; _rb.linearVelocity = v;
        _rb.angularVelocity = Vector3.zero;

        health -= amount;
        if (health <= 0)
            GameManager.instance.EndGame();
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

    #region Public API

    public void SetPipeLogic(PipeLogic targetPipe) => _pipe = targetPipe;
    public void SetKickState(bool value) => IsKicking = value;

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position - new Vector3(0f, kickHeightOffset, 0f), kickRange);
    }

    #endregion
}