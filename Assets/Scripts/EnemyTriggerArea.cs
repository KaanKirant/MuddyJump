using System.Collections;
using UnityEngine;

/// <summary>
/// Trigger volume that detects incoming pipe sweeps and tells EnemyAI to react.
/// Attach this as a child of the enemy prefab with a trigger Collider sized to
/// the enemy's "danger zone" — roughly the radius the pipe sweeps through.
///
/// Only reacts to the main (non-lethal) pipe. The elevated second pipe is
/// lethal and instant-kill — there is no kick reaction to it, only dodge.
///
/// Reaction time scales inversely with pipe speed so the AI stays appropriately
/// challenging at high difficulty without becoming impossible at low speed.
/// </summary>
public class EnemyTriggerArea : MonoBehaviour
{
    [Range(0.05f, 0.4f)]
    [Tooltip("Reaction time at minimum pipe speed. Automatically shrinks as pipe speeds up.")]
    public float baseReactionTime = 0.15f;

    private EnemyAI _parentLogic;

    private void Awake()
    {
        // Awake instead of Start — trigger can fire on the first frame the enemy is alive
        _parentLogic = GetComponentInParent<EnemyAI>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Pipe")) return;

        PipeLogic pipe = other.GetComponentInParent<PipeLogic>();
        if (pipe == null || _parentLogic == null) return;

        // Lethal pipe (second pipe) is instant-kill — no kick reaction, skip entirely
        if (pipe.isLethalPipe) return;

        StartCoroutine(WaitToReact(pipe));
    }

    private IEnumerator WaitToReact(PipeLogic pipe)
    {
        float speed = Mathf.Max(pipe.BaseSpeed, 1f);
        float reactionTime = Mathf.Clamp(baseReactionTime * (50f / speed), 0.05f, baseReactionTime);

        yield return new WaitForSeconds(reactionTime);

        if (_parentLogic != null)
            _parentLogic.DecideAction();
    }
}