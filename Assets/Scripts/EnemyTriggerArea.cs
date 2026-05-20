using System.Collections;
using UnityEngine;

/// <summary>
/// Trigger volume that detects incoming pipe sweeps and tells EnemyAI to react.
/// Attach this as a child of the enemy prefab with a trigger Collider sized to
/// the enemy's "danger zone" — roughly the radius the pipe sweeps through.
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

        // Walk up to the PipeLogic root — the pipe collider may be a child mesh
        PipeLogic pipe = other.GetComponentInParent<PipeLogic>();
        if (pipe == null || _parentLogic == null) return;

        StartCoroutine(WaitToReact(pipe));
    }

    private IEnumerator WaitToReact(PipeLogic pipe)
    {
        // Use BaseSpeed — the difficulty floor set by GameManager each frame
        float speed = Mathf.Max(pipe.BaseSpeed, 1f);
        float reactionTime = Mathf.Clamp(baseReactionTime * (50f / speed), 0.05f, baseReactionTime);

        yield return new WaitForSeconds(reactionTime);

        // Enemy may have been destroyed during the reaction window
        if (_parentLogic != null)
            _parentLogic.DecideAction();
    }
}