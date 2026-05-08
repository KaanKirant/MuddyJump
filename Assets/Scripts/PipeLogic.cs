using System.Collections;
using UnityEngine;

public class PipeLogic : MonoBehaviour
{
    public float rotationSpeed = 50f;
    public float rotationSpeedMultiplier = 1.25f;
    public bool rotationDirection = true;   // true = clockwise (+Y)

    [Header("Cooldowns")]
    public float kickCooldown = 0.5f;

    [Header("Speed Clamp")]
    [SerializeField] private float minSpeed = 25f;
    [SerializeField] private float maxSpeed = 200f;

    private bool _kickOnCooldown;
    private bool _hitOnCooldown;

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

            PlayHitReaction(collision.gameObject);
            player.TakeDamage(1);
            OnHitResolve();
        }
        else if (collision.gameObject.CompareTag("Enemy"))
        {
            EnemyAI enemy = collision.gameObject.GetComponentInParent<EnemyAI>();
            if (enemy == null || enemy.isInvincible) return;

            PlayHitReaction(collision.gameObject);
            enemy.TakeDamage(1);
            OnHitResolve();
        }
    }

    private void PlayHitReaction(GameObject go)
    {
        Animator anim = go.GetComponentInParent<Animator>()
                     ?? go.GetComponentInChildren<Animator>();
        if (anim != null)
            anim.Play(rotationDirection ? "HitReactionRight" : "HitReactionLeft", 1);
    }

    private void OnHitResolve()
    {
        rotationDirection = !rotationDirection;
        rotationSpeed = Mathf.Clamp(rotationSpeed / rotationSpeedMultiplier, minSpeed, maxSpeed);
        StartCoroutine(HitCooldown());
    }

    // Returns true if the kick connected (correct direction, not on cooldown)
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
}