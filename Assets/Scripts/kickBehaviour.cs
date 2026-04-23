using UnityEngine;

public class kickBehaviour : StateMachineBehaviour
{
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        PlayerMovement playerMovement = animator.transform.GetComponentInParent<PlayerMovement>();
        playerMovement.isKicking = true;
    }

    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        PlayerMovement playerMovement = animator.transform.GetComponentInParent<PlayerMovement>();
        playerMovement.isKicking = false;
    }
}