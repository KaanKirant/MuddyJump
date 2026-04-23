using UnityEngine;
using System.Collections;

public class EnemyTriggerArea : MonoBehaviour
{
    public float distanceToPipe = 5.0f;

    private EnemyAI parentLogic;

    private void Start()
    {
        parentLogic = GetComponentInParent<EnemyAI>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Pipe")) return;

        PipeLogic pipe = other.GetComponentInParent<PipeLogic>();
        if (pipe != null && parentLogic != null)
            StartCoroutine(WaitToReact(pipe));
    }

    private IEnumerator WaitToReact(PipeLogic pipe)
    {
        // Higher speed = shorter wait time. Formula: distance / speed
        float waitTime = Mathf.Clamp(distanceToPipe / Mathf.Max(pipe.rotationSpeed, 1f), 0.05f, 0.8f);
        yield return new WaitForSeconds(waitTime);
        parentLogic.DecideAction();
    }
}