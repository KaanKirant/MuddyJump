using UnityEngine;

// Tracks whether a kick animation is active on either the player or enemy.
// The isKicking flag is no longer used for hit detection (that's handled
// by OnKickImpact via Animation Events) but is kept for other systems
// that may want to know if a kick anim is playing (e.g. UI, combos).
public class kickBehaviour : StateMachineBehaviour
{
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        SetKicking(animator, true);
    }

    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        SetKicking(animator, false);
    }

    private void SetKicking(Animator animator, bool value)
    {
        PlayerMovement player = animator.transform.GetComponentInParent<PlayerMovement>();
        if (player != null) { player.isKicking = value; return; }

        EnemyAI enemy = animator.transform.GetComponentInParent<EnemyAI>();
        if (enemy != null) enemy.isKicking = value;
    }
}