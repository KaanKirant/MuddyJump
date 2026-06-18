using System.Collections;
using UnityEngine;

/// <summary>
/// Trigger volume that detects incoming pipe sweeps and tells the parent
/// EnemyAI to decide whether to kick or dodge.
///
/// Attach as a child of the enemy prefab with a trigger Collider sized to the
/// enemy's "danger zone" — roughly the arc the pipe sweeps through.
///
/// Only reacts to the main (non-lethal) pipe. The elevated second pipe is lethal
/// and instant-kill — no kick reaction is possible, so those triggers are ignored.
///
/// Reaction time scales inversely with pipe speed so AI behaviour stays
/// appropriately challenging at high difficulty without being unfair at low speed.
///
/// Inspector setup:
///   baseReactionTime — delay at minimum pipe speed; clamped shorter at high speed
/// </summary>
public class EnemyTriggerArea : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Range(0.05f, 0.4f)]
    [Tooltip("Reaction delay at minimum pipe speed. Auto-shortened as the pipe speeds up.")]
    public float baseReactionTime = 0.15f;

    // ─── Private ──────────────────────────────────────────────────────────────
    private EnemyAI _parentLogic;

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        // Use Awake — the trigger can fire on the very first frame the enemy is alive.
        _parentLogic = GetComponentInParent<EnemyAI>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Pipe")) return;

        PipeLogic pipe = other.GetComponentInParent<PipeLogic>();
        if (pipe == null || _parentLogic == null) return;

        // Lethal pipe = instant kill, no kick reaction.
        if (pipe.isLethalPipe) return;

        StartCoroutine(WaitToReact(pipe));
    }

    // ─── Private ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Waits a speed-scaled reaction window then tells the enemy to act.
    /// Faster pipe → shorter reaction time (harder to evade) but never below 0.05 s.
    /// </summary>
    private IEnumerator WaitToReact(PipeLogic pipe)
    {
        float speed = Mathf.Max(pipe.BaseSpeed, 1f);
        float reactionTime = Mathf.Clamp(baseReactionTime * (50f / speed), 0.05f, baseReactionTime);

        yield return new WaitForSeconds(reactionTime);

        _parentLogic?.DecideAction();
    }
}