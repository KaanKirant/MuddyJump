using UnityEngine;

public class PipeLogic : MonoBehaviour
{
    //Should speed be constant levels (like speed level 1 = 25, level 2 = 35 ...) or multiply and divide based on player and enemy interactions?
    public float rotationSpeed = 50f;
    public float rotationSpeedMultiplier = 1.25f; // Multiplier for increasing speed on kick
    public bool rotationDirection = true; // true for clockwise, false for counterclockwise
    public bool isHitArea = false;
    void Update()
    {
        if (rotationDirection)
            transform.Rotate(0, rotationSpeed * Time.deltaTime, 0);
        else
            transform.Rotate(0, -rotationSpeed * Time.deltaTime, 0);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            // Call a function on the effected gameobject to reduce health, and check if it's dead
            collision.gameObject.GetComponentInParent<PlayerMovement>().health -= 1; // Reduce player's health by 1 on collision with player
            if (rotationDirection)
            {
                collision.gameObject.GetComponent<Animator>().Play("HitReactionRight", 1); // Play hit reaction animation on the player
            }
            else
            {
                collision.gameObject.GetComponent<Animator>().Play("HitReactionLeft", 1); // Play hit reaction animation on the player
            }
            if (collision.gameObject.GetComponentInParent<PlayerMovement>().health <= 0)
            {
                // Handle player death (e.g., disable player, show game over screen, etc.)
                GameManager.instance.EndGame();
                //Play Death Animation here
            }

            rotationDirection =  !rotationDirection; // Change rotation direction on collision with player
            rotationSpeed = rotationSpeed / rotationSpeedMultiplier; // Reduce rotation speed by half on collision with player
            rotationSpeed = Mathf.Clamp(rotationSpeed, 25f, 100f);
        }
        else if (collision.gameObject.CompareTag("Enemy"))
        {
            // Call a function on the effected gameobject to reduce health, and check if it's dead
            collision.gameObject.GetComponent<EnemyAI>().health -= 1; // Reduce enemy's health by 1 on collision with player
            if (rotationDirection)
            {
                //collision.gameObject.GetComponent<Animator>().Play("HitReactionRight", 1); // Play hit reaction animation on the enemy
            }
            else
            {
                //collision.gameObject.GetComponent<Animator>().Play("HitReactionLeft", 1); // Play hit reaction animation on the enemy
            }

            if (collision.gameObject.GetComponent<EnemyAI>().health <= 0)
            {
                // Handle enemy death (e.g., disable enemy, show death animation, etc.)
                Debug.Log("Enemy has died!");
                //Play death animation here
            }

            rotationDirection = !rotationDirection; // Change rotation direction on collision with player
            rotationSpeed = rotationSpeed / rotationSpeedMultiplier; // Reduce rotation speed by half on collision with player
            rotationSpeed = Mathf.Clamp(rotationSpeed, 25f, 100f);
        }
    }

    public void GetKicked(Vector2 direction)
    {
        if (direction.x == 1 && rotationDirection && isHitArea)
        {
            Debug.Log("Kicked right!");
            rotationDirection = !rotationDirection;
            rotationSpeed = rotationSpeed * rotationSpeedMultiplier;
            rotationSpeed = Mathf.Clamp(rotationSpeed, 25f, 100f);
        }
        else if(direction.x == -1 && !rotationDirection && isHitArea)
        {
            Debug.Log("Kicked left!");
            rotationDirection = !rotationDirection;
            rotationSpeed = rotationSpeed * rotationSpeedMultiplier;
            rotationSpeed = Mathf.Clamp(rotationSpeed, 25f, 100f);
        }
    }
}