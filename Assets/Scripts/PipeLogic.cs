using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rotates continuously and reacts to kicks and hits.
///
/// Speed model — two separate values:
///   BaseSpeed     — set by GameManager every frame as the difficulty floor.
///   _runtimeSpeed — the live rotation speed, modified by kicks (+) and hits (−).
///                   Decays back toward BaseSpeed over time so the pipe
///                   never stays artificially fast or slow forever.
///
/// This separation means kicks and hits have a lasting effect instead of being
/// silently overwritten on the next GameManager Update tick.
///
/// Direction convention: rotationDirection = true → clockwise (+Y axis).
///
/// Cross-pipe protection:
///   _recentlyHitTargets is a static HashSet shared across all PipeLogic
///   instances, preventing two pipes from double-hitting the same target in
///   the same cooldown window. It is cleared in OnDestroy to avoid leaking
///   stale instance IDs across scene reloads.
/// </summary>
public class PipeLogic : MonoBehaviour
{
    // ─── Speed ────────────────────────────────────────────────────────────────
    [Header("Speed")]
    [Tooltip("Difficulty floor written by GameManager every frame. " +
             "Do not set this directly — use GameManager pipe speed settings.")]
    public float BaseSpeed = 60f;

    [Tooltip("Speed multiplier per successful kick. 1.4 = 40% faster per kick.")]
    public float kickSpeedMultiplier = 1.4f;

    [Tooltip("Speed divisor per hit. 1.3 = 30% slower per hit.")]
    public float hitSpeedDivisor = 1.3f;

    [Tooltip("Rate at which _runtimeSpeed decays back to BaseSpeed (units/second). " +
             "Higher = snappier recovery. 0 = no decay.")]
    public float speedDecayRate = 8f;

    [Header("Speed Clamp")]
    [Tooltip("Absolute minimum rotation speed regardless of hits.")]
    [SerializeField] private float minSpeed = 30f;

    [Tooltip("Absolute maximum rotation speed regardless of kicks.")]
    [SerializeField] private float maxSpeed = 300f;

    /// <summary>Live rotation speed in degrees per second. Modified by kicks and hits.</summary>
    public float RuntimeSpeed => _runtimeSpeed;

    // ─── Pipe Type ────────────────────────────────────────────────────────────
    [Header("Pipe Type")]
    [Tooltip("If true, contact instant-kills on contact. No kick interaction. " +
             "Used for the elevated second pipe.")]
    public bool isLethalPipe = false;

    [Tooltip("Child transform at the tip of the pipe arm. Used by EnemyAI for arrival-time calculation.")]
    public Transform pipeTip;

    // ─── Cooldowns ────────────────────────────────────────────────────────────
    [Header("Cooldowns")]
    [Tooltip("Seconds after a hit before this pipe can damage again. Prevents chain hits.")]
    public float hitCooldown = 0.5f;

    [Tooltip("Seconds after a kick before another kick registers. Prevents kick spam.")]
    public float kickCooldown = 0.3f;

    // ─── State ────────────────────────────────────────────────────────────────
    /// <summary>
    /// Current rotation direction. true = clockwise (+Y).
    /// Read by EnemyAI and PlayerMovement to determine the valid kick direction.
    /// </summary>
    public bool rotationDirection = true;

    // ─── Private ──────────────────────────────────────────────────────────────
    /// <summary>Live speed — modified by kicks and hits, decays toward BaseSpeed.</summary>
    private float _runtimeSpeed;

    private bool _kickOnCooldown;
    private bool _hitOnCooldown;

    /// <summary>
    /// Shared across all PipeLogic instances to prevent double-hits when two
    /// pipes overlap the same target in the same cooldown window.
    /// Cleared on pipe destruction to prevent stale IDs crossing scene reloads.
    /// </summary>
    private static readonly HashSet<int> _recentlyHitTargets = new HashSet<int>();

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _runtimeSpeed = BaseSpeed;
    }

    private void OnDestroy()
    {
        // Static sets persist across scene reloads in the Editor.
        // Clear when the owning pipe is destroyed so stale IDs don't ghost.
        _recentlyHitTargets.Clear();
    }

    private void Update()
    {
        // Decay _runtimeSpeed toward BaseSpeed — kicks/hits have lasting but not permanent effect.
        if (speedDecayRate > 0f)
            _runtimeSpeed = Mathf.MoveTowards(_runtimeSpeed, BaseSpeed, speedDecayRate * Time.deltaTime);

        // Re-clamp every frame in case BaseSpeed was raised by GameManager.
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

            Camera.main?.GetComponent<CameraController>()?.TriggerShake(0.08f, 0.2f);
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

            Camera.main?.GetComponent<CameraController>()?.TriggerShake(0.08f, 0.2f);
            GameManager.instance?.TriggerHitStop(0.1f, 0.04f);
            StartCoroutine(LockTarget(id));
        }
    }

    // ─── Hit / Kick Resolution ────────────────────────────────────────────────

    /// <summary>
    /// Plays the appropriate hit reaction animation on the target.
    /// Searches parent first (collider is often a child), then children.
    /// </summary>
    private void PlayHitReaction(GameObject go)
    {
        Animator anim = go.GetComponentInParent<Animator>()
                     ?? go.GetComponentInChildren<Animator>();
        anim?.Play(rotationDirection ? "HitReactionRight" : "HitReactionLeft", 1, 0f);
    }

    /// <summary>
    /// Called when the pipe successfully hits a target.
    /// Reverses direction and slows the pipe — reward for surviving a hit.
    /// </summary>
    private void ResolveHit()
    {
        rotationDirection = !rotationDirection;
        _runtimeSpeed = Mathf.Clamp(_runtimeSpeed / hitSpeedDivisor, minSpeed, maxSpeed);
        StartCoroutine(HitCooldownRoutine());
    }

    /// <summary>
    /// Called by PlayerMovement.CheckKickContact() and EnemyAI.CheckKickContact().
    /// Returns true if the kick landed (correct direction, not on cooldown).
    /// On success: reverses direction and increases runtime speed.
    /// The speed increase persists — GameManager only writes BaseSpeed, not _runtimeSpeed.
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

    /// <summary>
    /// Temporarily halts the pipe. Intended for future power-up use.
    /// Saved speed is restored after the duration so no permanent state change occurs.
    /// </summary>
    public void Freeze(float duration) => StartCoroutine(FreezeCoroutine(duration));

    // ─── Coroutines ───────────────────────────────────────────────────────────

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

    /// <summary>
    /// Adds the target's instance ID to the cross-pipe protection set for one
    /// hitCooldown window, then removes it. This prevents two pipes from hitting
    /// the same character in the same sweep.
    /// </summary>
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
}