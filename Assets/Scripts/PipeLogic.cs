using UnityEngine;

public class PipeLogic : MonoBehaviour
{
    public float rotationSpeed = 50f;
    public float rotationSpeedMultiplier = 1.25f;
    public bool rotationDirection = true; // true = clockwise, false = counter-clockwise
    public bool isHitArea = false;

    private void Update()
    {
        float direction = rotationDirection ? 1f : -1f;
        transform.Rotate(0f, rotationSpeed * direction * Time.deltaTime, 0f);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerMovement player = collision.gameObject.GetComponentInParent<PlayerMovement>();
            Animator playerAnimator = collision.gameObject.GetComponent<Animator>();

            player.health -= 1;
            playerAnimator.Play(rotationDirection ? "HitReactionRight" : "HitReactionLeft", 1);

            if (player.health <= 0)
                GameManager.instance.EndGame();

            OnHitResolve();
        }
        else if (collision.gameObject.CompareTag("Enemy"))
        {
            EnemyAI enemy = collision.gameObject.GetComponent<EnemyAI>();

            enemy.health -= 1;

            OnHitResolve();
        }
    }

    // Shared post-hit logic: reverse direction and reduce speed
    private void OnHitResolve()
    {
        rotationDirection = !rotationDirection;
        rotationSpeed = Mathf.Clamp(rotationSpeed / rotationSpeedMultiplier, 25f, 100f);
    }

    public void GetKicked(Vector2 direction)
    {
        bool kickingRight = direction.x > 0f && rotationDirection;
        bool kickingLeft = direction.x < 0f && !rotationDirection;

        if ((kickingRight || kickingLeft) && isHitArea)
        {
            Debug.Log(kickingRight ? "Kicked right!" : "Kicked left!");
            rotationDirection = !rotationDirection;
            rotationSpeed = Mathf.Clamp(rotationSpeed * rotationSpeedMultiplier, 25f, 100f);
        }
    }
}