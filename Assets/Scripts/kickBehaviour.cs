using UnityEngine;

// StateMachineBehaviour that tracks kick animation state on both player and enemy.
// Attach to any kick animation clip state in the Animator.
public class kickBehaviour : StateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        => SetKicking(animator, true);

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        => SetKicking(animator, false);

    private static void SetKicking(Animator animator, bool value)
    {
        // Try player first, then enemy — avoids redundant GetComponent calls
        PlayerMovement player = animator.GetComponentInParent<PlayerMovement>();
        if (player != null) { player.SetKickState(value); return; }

        EnemyAI enemy = animator.GetComponentInParent<EnemyAI>();
        if (enemy != null) enemy.isKicking = value;
    }
}