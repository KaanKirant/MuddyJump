using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rotates continuously and reacts to kicks and hits.
///
/// Fix — hit reaction timing:
///   anim.Play(state, layer, 0f) forces immediate playback from time=0.
///   Previously called without normalizedTime, which allowed transition
///   blending to delay the reaction visually.
///
/// Fix — two-pipe same-target problem:
///   A per-target HashSet (_recentlyHitTargets) prevents two pipes from
///   damaging the same player/enemy within a single hit window.
///   Each pipe has its own PipeLogic instance so cooldown flags are already
///   independent — this adds the cross-pipe protection on the target side.
/// </summary>
public class PipeLogic : MonoBehaviour
{
    public float rotationSpeed = 50f;
    public float rotationSpeedMultiplier = 1.25f;

    /// <summary>true = clockwise (+Y). Read by EnemyAI and PlayerMovement to decide kick direction.</summary>
    public bool rotationDirection = true;

    [Header("Cooldowns")]
    public float kickCooldown = 0.5f;

    [Header("Speed Clamp")]
    [SerializeField] private float minSpeed = 25f;
    [SerializeField] private float maxSpeed = 200f;

    private bool _kickOnCooldown;
    private bool _hitOnCooldown;

    // Cross-pipe hit protection — shared across all PipeLogic instances via static.
    // When any pipe hits a target, that target's instance ID is locked for kickCooldown
    // seconds so the second pipe cannot deal damage in the same window.
    private static readonly HashSet<int> _recentlyHitTargets = new HashSet<int>();

    #region Unity Lifecycle

    private void Update()
    {
        float dir = rotationDirection ? 1f : -1f;
        transform.Rotate(0f, rotationSpeed * dir * Time.deltaTime, 0f);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_hitOnCooldown) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerMovement player = collision.gameObject.GetComponentInParent<PlayerMovement>();
            if (player == null || player.IsInvincible) return;

            // Cross-pipe guard — bail if another pipe already hit this player this frame
            int id = player.GetEntityId();
            if (_recentlyHitTargets.Contains(id)) return;

            PlayHitReaction(collision.gameObject);
            player.TakeDamage(1);
            ResolveHit();
            StartCoroutine(LockTarget(id));
        }
        else if (collision.gameObject.CompareTag("Enemy"))
        {
            EnemyAI enemy = collision.gameObject.GetComponentInParent<EnemyAI>();
            if (enemy == null || enemy.isInvincible) return;

            int id = enemy.GetEntityId();
            if (_recentlyHitTargets.Contains(id)) return;

            PlayHitReaction(collision.gameObject);
            enemy.TakeDamage(1);
            ResolveHit();
            StartCoroutine(LockTarget(id));
        }
    }

    #endregion

    #region Hit / Kick Resolution

    private void PlayHitReaction(GameObject go)
    {
        Animator anim = go.GetComponentInParent<Animator>()
                     ?? go.GetComponentInChildren<Animator>();

        if (anim == null) return;

        // normalizedTime = 0f forces the state to start immediately from the beginning,
        // bypassing any transition blend time — makes the reaction feel instant
        anim.Play(rotationDirection ? "HitReactionRight" : "HitReactionLeft", 1, 0f);
    }

    private void ResolveHit()
    {
        rotationDirection = !rotationDirection;
        rotationSpeed = Mathf.Clamp(rotationSpeed / rotationSpeedMultiplier, minSpeed, maxSpeed);
        StartCoroutine(HitCooldown());
    }

    public bool GetKicked(Vector2 direction)
    {
        if (_kickOnCooldown) return false;

        bool kickingRight = direction.x > 0f && rotationDirection;
        bool kickingLeft = direction.x < 0f && !rotationDirection;
        if (!kickingRight && !kickingLeft) return false;

        rotationDirection = !rotationDirection;
        rotationSpeed = Mathf.Clamp(rotationSpeed * rotationSpeedMultiplier, minSpeed, maxSpeed);
        StartCoroutine(KickCooldown());
        return true;
    }

    public void Freeze(float duration) => StartCoroutine(FreezeCoroutine(duration));

    #endregion

    #region Coroutines

    private IEnumerator KickCooldown()
    {
        _kickOnCooldown = true;
        yield return new WaitForSeconds(kickCooldown);
        _kickOnCooldown = false;
    }

    private IEnumerator HitCooldown()
    {
        _hitOnCooldown = true;
        yield return new WaitForSeconds(kickCooldown);
        _hitOnCooldown = false;
    }

    // Locks a target's instance ID in the shared set for kickCooldown seconds.
    // Any other pipe that tries to hit this target during the window will be rejected.
    private IEnumerator LockTarget(int instanceId)
    {
        _recentlyHitTargets.Add(instanceId);
        yield return new WaitForSeconds(kickCooldown);
        _recentlyHitTargets.Remove(instanceId);
    }

    private IEnumerator FreezeCoroutine(float duration)
    {
        float saved = rotationSpeed;
        rotationSpeed = 0f;
        yield return new WaitForSeconds(duration);
        rotationSpeed = saved;
    }

    #endregion
}