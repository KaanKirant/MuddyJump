using UnityEngine;
using System.Collections;

public class PipeLogic : MonoBehaviour
{
    public float rotationSpeed = 50f;
    public float rotationSpeedMultiplier = 1.25f;
    public bool rotationDirection = true;

    [Header("Cooldowns")]
    public float kickCooldown = 0.5f;

    private bool kickOnCooldown = false;
    private bool hitOnCooldown = false;

    private void Update()
    {
        float dir = rotationDirection ? 1f : -1f;
        transform.Rotate(0f, rotationSpeed * dir * Time.deltaTime, 0f);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hitOnCooldown) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerMovement player = collision.gameObject.GetComponentInParent<PlayerMovement>();
            if (player == null) return;

            // Skip entirely if player is in kick invincibility window
            if (player.IsInvincible) return;

            Animator anim = GetAnimator(collision.gameObject);
            if (anim != null)
                anim.Play(rotationDirection ? "HitReactionRight" : "HitReactionLeft", 1);

            player.TakeDamage(1);
            OnHitResolve();
        }
        else if (collision.gameObject.CompareTag("Enemy"))
        {
            EnemyAI enemy = collision.gameObject.GetComponentInParent<EnemyAI>();
            if (enemy == null) return;

            // Skip entirely if enemy is in kick invincibility window
            if (enemy.isInvincible) return;

            Animator anim = GetAnimator(collision.gameObject);
            if (anim != null)
                anim.Play(rotationDirection ? "HitReactionRight" : "HitReactionLeft", 1);

            enemy.TakeDamage(1);
            OnHitResolve();
        }
    }

    private void OnHitResolve()
    {
        rotationDirection = !rotationDirection;
        rotationSpeed = Mathf.Clamp(rotationSpeed / rotationSpeedMultiplier, 25f, 200f);
        StartCoroutine(HitCooldown());
    }

    public bool GetKicked(Vector2 direction)
    {
        if (kickOnCooldown) return false;

        bool kickingRight = direction.x > 0f && rotationDirection;
        bool kickingLeft = direction.x < 0f && !rotationDirection;

        if (kickingRight || kickingLeft)
        {
            rotationDirection = !rotationDirection;
            rotationSpeed = Mathf.Clamp(rotationSpeed * rotationSpeedMultiplier, 25f, 200f);
            StartCoroutine(KickCooldown());
            return true;
        }

        return false;
    }

    // Helper to find Animator anywhere in the hierarchy of the collided object
    private Animator GetAnimator(GameObject go)
    {
        Animator anim = go.GetComponentInParent<Animator>();
        if (anim == null) anim = go.GetComponentInChildren<Animator>();
        return anim;
    }

    private IEnumerator KickCooldown()
    {
        kickOnCooldown = true;
        yield return new WaitForSeconds(kickCooldown);
        kickOnCooldown = false;
    }

    private IEnumerator HitCooldown()
    {
        hitOnCooldown = true;
        yield return new WaitForSeconds(kickCooldown);
        hitOnCooldown = false;
    }

    public void Freeze(float duration)
    {
        StartCoroutine(FreezeCoroutine(duration));
    }

    private IEnumerator FreezeCoroutine(float duration)
    {
        float saved = rotationSpeed;
        rotationSpeed = 0f;
        yield return new WaitForSeconds(duration);
        rotationSpeed = saved;
    }
}