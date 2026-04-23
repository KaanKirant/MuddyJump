using UnityEngine;
using System.Collections;

public class EnemyAI : MonoBehaviour
{
    public GameObject pipeObject;
    public int health = 3;
    public bool isPipeClose = false;
    public float distanceToWait = 25.0f;

    private void Awake()
    {
        pipeObject = GameObject.FindGameObjectWithTag("Pipe");
    }

    private void Jump()
    {
        GetComponent<Rigidbody>().AddForce(Vector3.up * 5f, ForceMode.Impulse);
    }

    private void Kick(Vector2 direction)
    {
        pipeObject.GetComponent<PipeLogic>().GetKicked(direction);
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
}