using UnityEngine;
using System.Collections;

public class EnemyAI : MonoBehaviour
{
    public GameObject pipeObject;
    public int health = 3;
    public bool isPipeClose = false;
    public float distanceToWait = 25.0f;

    private Animator animator;

    [SerializeField] private bool isGrounded;


    private void Awake()
    {
        pipeObject = GameObject.FindGameObjectWithTag("Pipe");
        animator = GetComponent<Animator>();
    }

    private void Jump()
    {
        GetComponent<Rigidbody>().AddForce(Vector3.up * 5f, ForceMode.Impulse);
        animator.Update(0);
        animator.Play("Jump", 0, 0f);
    }

    private void Kick(Vector2 direction)
    {
        pipeObject.GetComponent<PipeLogic>().GetKicked(direction);
        if(direction == Vector2.right)
        {
            animator.Update(0);
            animator.Play("kickRight", 0, 0f);
        }
        else if(direction == Vector2.left)
        {
            animator.Update(0);
            animator.Play("kickLeft", 0, 0f);
        }
    }

    public void DecideAction()
    {
        float chance = Random.value;

        if (chance < 0.5f)
            StartCoroutine(TimedKick());
        else
            Jump();
    }

    // Called from PipeLogic instead of directly subtracting health
    public void TakeDamage(int amount)
    {
        health -= amount;

        if (health <= 0)
            SpawnManager.instance.OnEnemyDied(gameObject);
    }

    private IEnumerator TimedKick()
    {
        PipeLogic pipe = pipeObject.GetComponent<PipeLogic>();
        float delay = Mathf.Clamp(distanceToWait / Mathf.Max(pipe.rotationSpeed, 1f), 0.05f, 0.5f);
        yield return new WaitForSeconds(delay);

        Vector2 direction = pipe.rotationDirection ? Vector2.right : Vector2.left;
        Kick(direction);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            if (!isGrounded)
                animator.Play("Idle", 0, 0.1f);

            isGrounded = true;
            animator.SetBool("isGround", true);
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = false;
            animator.SetBool("isGround", false);
        }
    }
}