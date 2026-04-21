using UnityEngine;
using System.Collections;

public class EnemyTriggerArea : MonoBehaviour
{
    private EnemyAI parentLogic;
    // Adjust this to fine-tune the "Sweet Spot"
    public float distanceToPipe = 5.0f;
    void Start()
    {
        parentLogic = GetComponentInParent<EnemyAI>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Pipe"))
        {
            PipeLogic pipe = other.GetComponentInParent<PipeLogic>();
            if (pipe != null && parentLogic != null)
            {
                // Start the brain processing
                StartCoroutine(WaitToReact(pipe));
            }
        }
    }

    IEnumerator WaitToReact(PipeLogic pipe)
    {
        // 1. Calculate wait time: Higher speed = Lower wait time
        // Formula: (Constant / Speed)
        // If speed is 100, wait 0.1s. If speed is 10, wait 1.0s.
        float waitTime = distanceToPipe / Mathf.Max(pipe.rotationSpeed, 1f);

        // 2. Clamp the wait time so it's never too crazy
        waitTime = Mathf.Clamp(waitTime, 0.05f, 0.8f);

        yield return new WaitForSeconds(waitTime);

        // 3. Now perform the action at the right moment!
        parentLogic.DecideAction();
    }
}
