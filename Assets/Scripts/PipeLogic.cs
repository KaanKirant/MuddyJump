using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rotates continuously and reacts to kicks and hits.
///
/// Speed model — two separate values:
///   BaseSpeed      set by GameManager every frame as the difficulty floor.
///   _runtimeSpeed  the live rotation speed, modified by kicks (+) and hits (-).
///                  Decays back toward BaseSpeed over time so the pipe
///                  never stays artificially fast or slow forever.
///
/// This separation means kicks and hits actually have a lasting effect
/// instead of being overwritten on the next GameManager Update tick.
///
/// Direction convention: rotationDirection = true → clockwise (+Y axis).
/// </summary>
public class PipeLogic : MonoBehaviour
{
    // ─── Speed ────────────────────────────────────────────────────────────────
    [Header("Speed")]
    [Tooltip("Set by GameManager each frame as the difficulty floor. " +
             "Do not key-frame or set this directly — use GameManager pipe speed settings.")]
    public float BaseSpeed = 60f;

    [Tooltip("How strongly each successful kick multiplies the runtime speed. " +
             "1.4 = 40% faster per kick.")]
    public float kickSpeedMultiplier = 1.4f;

    [Tooltip("How strongly each hit divides the runtime speed. " +
             "1.3 = 30% slower per hit.")]
    public float hitSpeedDivisor = 1.3f;

    [Tooltip("How fast runtime speed decays back toward BaseSpeed (units per second). " +
             "Higher = snappier recovery. 0 = no decay.")]
    public float speedDecayRate = 8f;

    [Header("Speed Clamp")]
    [Tooltip("Absolute minimum rotation speed regardless of hits.")]
    [SerializeField] private float minSpeed = 30f;
    [Tooltip("Absolute maximum rotation speed regardless of kicks.")]
    [SerializeField] private float maxSpeed = 300f;

    public float RuntimeSpeed => _runtimeSpeed;

    // ─── Pipe Type ────────────────────────────────────────────────────────────
    [Header("Pipe Type")]
    [Tooltip("If true this pipe instant-kills on contact. No kicking possible. " +
             "Used for the elevated second pipe.")]
    public bool isLethalPipe = false;

    [Tooltip("Empty child transform at the tip of the pipe arm. Used by EnemyAI for arrival timing.")]
    public Transform pipeTip;

    // ─── Cooldowns ────────────────────────────────────────────────────────────
    [Header("Cooldowns")]
    [Tooltip("Seconds after a hit before this pipe can damage again. Prevents chain hits.")]
    public float hitCooldown = 0.5f;

    [Tooltip("Seconds after a kick before another kick registers. Prevents kick spam.")]
    public float kickCooldown = 0.3f;

    // ─── State ────────────────────────────────────────────────────────────────
    /// <summary>true = clockwise (+Y). Read by EnemyAI and PlayerMovement to decide kick direction.</summary>
    public bool rotationDirection = true;

    // ─── Private ──────────────────────────────────────────────────────────────
    private float _runtimeSpeed;   // Live speed — modified by kicks and hits, decays to BaseSpeed

    private bool _kickOnCooldown;
    private bool _hitOnCooldown;

    // Cross-pipe hit protection — shared across all PipeLogic instances.
    // Prevents two pipes from hitting the same target in the same window.
    private static readonly HashSet<int> _recentlyHitTargets = new HashSet<int>();

    #region Unity Lifecycle

    private void Awake()
    {
        _runtimeSpeed = BaseSpeed;
    }

    private void Update()
    {
        // Decay runtime speed back toward BaseSpeed — kicks/hits have lasting but not permanent effect
        if (speedDecayRate > 0f)
            _runtimeSpeed = Mathf.MoveTowards(_runtimeSpeed, BaseSpeed, speedDecayRate * Time.deltaTime);

        // Clamp in case BaseSpeed itself changed (GameManager ramps it every frame)
        _runtimeSpeed = Mathf.Clamp(_runtimeSpeed, minSpeed, maxSpeed);

        float dir = rotationDirection ? 1f : -1f;
        transform.Rotate(0f, _runtimeSpeed * dir * Time.deltaTime, 0f);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_hitOnCooldown) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerMovement player = collision.gameObject.GetComponentInParent<PlayerMovement>();
            if (player == null || player.IsInvincible) return;

            int id = player.GetHashCode();
            if (_recentlyHitTargets.Contains(id)) return;

            if (isLethalPipe)
            {
                player.InstantKill();
            }
            else
            {
                PlayHitReaction(collision.gameObject);
                player.TakeDamage(1);
                SoundManager.Instance?.PlaySFX(SoundType.PipeHitPlayer);
                ResolveHit();
            }

            CameraController cam = Camera.main?.GetComponent<CameraController>();
            cam?.TriggerShake(0.08f, 0.2f);
            GameManager.instance?.TriggerHitStop(0.1f, 0.04f);

            StartCoroutine(LockTarget(id));
        }
        else if (collision.gameObject.CompareTag("Enemy"))
        {
            EnemyAI enemy = collision.gameObject.GetComponentInParent<EnemyAI>();
            if (enemy == null || enemy.isInvincible) return;

            int id = enemy.GetHashCode();
            if (_recentlyHitTargets.Contains(id)) return;

            if (isLethalPipe)
            {
                enemy.InstantKill();
            }
            else
            {
                PlayHitReaction(collision.gameObject);
                enemy.TakeDamage(1);
                SoundManager.Instance?.PlaySFX(SoundType.PipeHitEnemy);
                ResolveHit();
            }

            CameraController cam = Camera.main?.GetComponent<CameraController>();
            cam?.TriggerShake(0.08f, 0.2f);
            GameManager.instance?.TriggerHitStop(0.1f, 0.04f);

            StartCoroutine(LockTarget(id));
        }
    }

    #endregion

    #region Hit / Kick Resolution

    private void PlayHitReaction(GameObject go)
    {
        Animator anim = go.GetComponentInParent<Animator>()
                     ?? go.GetComponentInChildren<Animator>();
        anim?.Play(rotationDirection ? "HitReactionRight" : "HitReactionLeft", 1, 0f);
    }

    /// <summary>
    /// Called when the pipe successfully hits a target.
    /// Slows the pipe down and reverses direction — reward for the player surviving.
    /// </summary>
    private void ResolveHit()
    {
        rotationDirection = !rotationDirection;
        _runtimeSpeed = Mathf.Clamp(_runtimeSpeed / hitSpeedDivisor, minSpeed, maxSpeed);
        StartCoroutine(HitCooldownRoutine());
    }

    /// <summary>
    /// Called by PlayerMovement.CheckKickContact() and EnemyAI.ResolveKickImpact().
    /// Returns true if the kick landed (correct direction, not on cooldown).
    /// On success: reverses direction and increases runtime speed.
    /// The speed increase persists — GameManager only controls BaseSpeed, not _runtimeSpeed.
    /// </summary>
    public bool GetKicked(Vector2 direction)
    {
        if (isLethalPipe) return false;
        if (_kickOnCooldown) return false;

        bool kickingRight = direction.x > 0f && rotationDirection;
        bool kickingLeft = direction.x < 0f && !rotationDirection;
        if (!kickingRight && !kickingLeft) return false;

        rotationDirection = !rotationDirection;
        _runtimeSpeed = Mathf.Clamp(_runtimeSpeed * kickSpeedMultiplier, minSpeed, maxSpeed);
        StartCoroutine(KickCooldownRoutine());
        return true;
    }

    /// <summary>Temporarily stops the pipe. Used by power-ups or special events.</summary>
    public void Freeze(float duration) => StartCoroutine(FreezeCoroutine(duration));

    #endregion

    #region Coroutines

    private IEnumerator KickCooldownRoutine()
    {
        _kickOnCooldown = true;
        yield return new WaitForSeconds(kickCooldown);
        _kickOnCooldown = false;
    }

    private IEnumerator HitCooldownRoutine()
    {
        _hitOnCooldown = true;
        yield return new WaitForSeconds(hitCooldown);
        _hitOnCooldown = false;
    }

    private IEnumerator LockTarget(int instanceId)
    {
        _recentlyHitTargets.Add(instanceId);
        yield return new WaitForSeconds(hitCooldown);
        _recentlyHitTargets.Remove(instanceId);
    }

    private IEnumerator FreezeCoroutine(float duration)
    {
        float saved = _runtimeSpeed;
        _runtimeSpeed = 0f;
        yield return new WaitForSeconds(duration);
        _runtimeSpeed = saved;
    }

    #endregion
}