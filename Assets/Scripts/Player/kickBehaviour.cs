using UnityEngine;

/// <summary>
/// StateMachineBehaviour attached to kick animation states in the Animator.
/// Keeps the IsKicking flag on PlayerMovement or EnemyAI in sync with the
/// animation state machine so code outside the animator always knows whether
/// a kick is visually playing.
///
/// Attach to: kickRight and kickLeft states on both player and enemy Animators.
/// No Inspector configuration needed — component is auto-detected from the hierarchy.
///
/// Naming note: Unity requires the class name to match the filename exactly.
/// This file is named kickBehaviour.cs to preserve the existing asset references
/// in the Animator. Rename both together if you want to follow PascalCase.
/// </summary>
public class kickBehaviour : StateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        => SetKicking(animator, true);

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        => SetKicking(animator, false);

    /// <summary>
    /// Finds the owning character component and updates its IsKicking flag.
    /// Checks player first (more common) so the enemy branch is only reached on enemy animators.
    /// </summary>
    private static void SetKicking(Animator animator, bool value)
    {
        PlayerMovement player = animator.GetComponentInParent<PlayerMovement>();
        if (player != null) { player.SetKickState(value); return; }

        EnemyAI enemy = animator.GetComponentInParent<EnemyAI>();
        if (enemy != null) enemy.isKicking = value;
    }
}