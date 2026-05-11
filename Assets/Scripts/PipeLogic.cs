using System.Collections;
using UnityEngine;

/// <summary>
/// Rotates continuously and reacts to kicks and hits.
///
/// Direction convention: rotationDirection = true → clockwise (+Y axis).
///
/// GetKicked(): called by PlayerMovement and EnemyAI at their impact frames.
///   Returns true if the kick landed (correct direction, not on cooldown).
///   Flips direction and increases speed on success.
///
/// OnCollisionEnter: damages player or enemy if they fail to dodge/kick.
///   Flips direction and decreases speed on hit.
/// </summary>
public class PipeLogic : MonoBehaviour
{
    public float rotationSpeed = 50f;
    public float rotationSpeedMultiplier = 1.25f;

    /// <summary>true = clockwise (+Y). Read by EnemyAI and PlayerMovement to decide kick direction.</summary>
    public bool rotationDirection = true;

    [Header("Cooldowns")]
    [Tooltip("Seconds after a kick or hit before another collision is processed. Prevents chain damage.")]
    public float kickCooldown = 0.5f;

    [Header("Speed Clamp")]
    [SerializeField] private float minSpeed = 25f;
    [SerializeField] private float maxSpeed = 200f;

    private bool _kickOnCooldown;
    private bool _hitOnCooldown;

    #region Unity Lifecycle

    private void Update()
    {
        // Constant rotation — speed and direction are mutated by kicks and hits
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

            PlayHitReaction(collision.gameObject);
            player.TakeDamage(1);

            // --- WHAT CHANGED --- Trigger hit stop for game feel
            if (GameManager.instance != null) GameManager.instance.TriggerHitStop();

            ResolveHit();
        }
        else if (collision.gameObject.CompareTag("Enemy"))
        {
            EnemyAI enemy = collision.gameObject.GetComponentInParent<EnemyAI>();
            if (enemy == null || enemy.isInvincible) return;

            PlayHitReaction(collision.gameObject);
            enemy.TakeDamage(1);

            // --- WHAT CHANGED --- Trigger hit stop for game feel
            if (GameManager.instance != null) GameManager.instance.TriggerHitStop();

            ResolveHit();
        }
    }

    #endregion

    #region Hit / Kick Resolution

    /// <summary>
    /// Plays the correct hit-reaction animation on layer 1 of the target's Animator.
    /// Searches parent first (character root), then children (sub-meshes).
    /// </summary>
    private void PlayHitReaction(GameObject go)
    {
        Animator anim = go.GetComponentInParent<Animator>()
                     ?? go.GetComponentInChildren<Animator>();
        anim?.Play(rotationDirection ? "HitReactionRight" : "HitReactionLeft", 1);
    }

    /// <summary>Called after a successful pipe hit. Slows pipe down and reverses direction.</summary>
    private void ResolveHit()
    {
        rotationDirection = !rotationDirection;
        rotationSpeed = Mathf.Clamp(rotationSpeed / rotationSpeedMultiplier, minSpeed, maxSpeed);
        StartCoroutine(HitCooldown());
    }

    /// <summary>
    /// Called by PlayerMovement.CheckKickContact() and EnemyAI.OnKickImpact().
    /// Returns true if the kick was valid and landed (correct direction, not on cooldown).
    /// On success: reverses direction and speeds up.
    /// On failure: returns false — caller decides whether to grant invincibility anyway.
    /// </summary>
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

    /// <summary>Temporarily stops the pipe. Used for power-ups or special events.</summary>
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

    private IEnumerator FreezeCoroutine(float duration)
    {
        float saved = rotationSpeed;
        rotationSpeed = 0f;
        yield return new WaitForSeconds(duration);
        rotationSpeed = saved;
    }

    #endregion
}