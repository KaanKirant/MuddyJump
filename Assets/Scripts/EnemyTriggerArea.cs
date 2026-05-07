using UnityEngine;
using System.Collections;

public class EnemyTriggerArea : MonoBehaviour
{
    // Base reaction time at minimum pipe speed.
    // Scales down automatically as pipe gets faster.
    [Range(0.05f, 0.4f)]
    public float baseReactionTime = 0.15f;

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
        // Faster pipe = shorter reaction window so the AI stays relevant
        // at high speeds. Minimum 0.05s so it never feels instant.
        float speed = Mathf.Max(pipe.rotationSpeed, 1f);
        float reactionTime = Mathf.Clamp(baseReactionTime * (50f / speed), 0.05f, baseReactionTime);

        yield return new WaitForSeconds(reactionTime);
        parentLogic.DecideAction();
    }
}