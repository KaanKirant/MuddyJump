using UnityEngine;

/// <summary>
/// StateMachineBehaviour attached to kick animation states in the Animator.
/// Syncs the IsKicking flag on PlayerMovement or EnemyAI when a kick
/// animation starts and ends.
///
/// Attach this to: kickRight, kickLeft states on both player and enemy Animators.
/// No configuration needed — it auto-detects which component is in the hierarchy.
/// </summary>
public class kickBehaviour : StateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        => SetKicking(animator, true);

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        => SetKicking(animator, false);

    private static void SetKicking(Animator animator, bool value)
    {
        // Player check first — more common, short-circuits before trying enemy
        PlayerMovement player = animator.GetComponentInParent<PlayerMovement>();
        if (player != null) { player.SetKickState(value); return; }

        EnemyAI enemy = animator.GetComponentInParent<EnemyAI>();
        if (enemy != null) enemy.isKicking = value;
    }
}