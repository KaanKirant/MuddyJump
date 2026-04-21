using UnityEngine;
using System.Collections;

public class EnemyAI : MonoBehaviour
{
    //Multiple pipes? For now just one, but should be able to handle multiple in the future
    public GameObject pipeObject;
    public int health = 3;
    public bool isPipeClose = false;
    public float distanceToWait = 25.0f; // Adjust this: smaller = kicks sooner

    private void Awake()
    {
        pipeObject = GameObject.FindGameObjectWithTag("Pipe");
    }

    private void Jump()
    {
        GetComponent<Rigidbody>().AddForce(Vector3.up * 5, ForceMode.Impulse);
    }

    private void Kick(Vector2 direction)
    {
        pipeObject.GetComponent<PipeLogic>().GetKicked(direction);
    }

    public void DecideAction()
    {
        //Should be changing trigger based on pipe speed for successful jumps and kicks, but for now it just jumps when the pipe is close
        //There should also be some randomization to make the enemy less predictable, but for now it just always
        //There should also chance of fail to make it more balanced, but for now it just always succeeds

        float chance = Random.value; // Get a number between 0 and 1

        if (chance < 0.5f)//0.4
        {
            // 40% chance to Kick
            StartCoroutine(TimedKick());
        }
        else if (chance < 1f)//0.7
        {
            // 30% chance to Jump (0.7 - 0.4 = 0.3)
            Jump();
        }
        else
        {
            // 30% chance to do nothing (Hesitate)
        }
    }

    IEnumerator TimedKick()
    {
        PipeLogic pipe = pipeObject.GetComponent<PipeLogic>();

        // 1. Calculate how fast the pipe is moving
        // We want to wait until the pipe is "right in front" of us.
        // If speed is high, wait is short. If speed is low, wait is long.
        float delay = distanceToWait / Mathf.Max(pipe.rotationSpeed, 1f);

        // Limit the delay so it doesn't wait forever if the pipe is super slow
        delay = Mathf.Clamp(delay, 0.05f, 0.5f);

        yield return new WaitForSeconds(delay);

        // 2. Perform the actual kick
        Vector2 direction = pipe.rotationDirection ? Vector2.right : Vector2.left;
        Kick(direction);
    }
}