using UnityEngine;

// Tracks whether a kick animation is active on either the player or enemy.
// Used for gameplay state, combo logic, UI, etc.
public class kickBehaviour : StateMachineBehaviour
{
    public override void OnStateEnter(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex)
    {
        SetKicking(animator, true);
    }

    public override void OnStateExit(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex)
    {
        SetKicking(animator, false);
    }

    private void SetKicking(Animator animator, bool value)
    {
        PlayerMovement player =
            animator.transform.GetComponentInParent<PlayerMovement>();

        if (player != null)
        {
            player.SetKickState(value);
            return;
        }

        EnemyAI enemy =
            animator.transform.GetComponentInParent<EnemyAI>();

        if (enemy != null)
        {
            enemy.isKicking = value;
        }
    }
}