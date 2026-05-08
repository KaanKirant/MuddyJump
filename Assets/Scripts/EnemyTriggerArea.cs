using System.Collections;
using UnityEngine;

public class EnemyTriggerArea : MonoBehaviour
{
    [Range(0.05f, 0.4f)]
    [Tooltip("Base reaction time at minimum pipe speed. Scales down as pipe accelerates.")]
    public float baseReactionTime = 0.15f;

    private EnemyAI _parentLogic;

    private void Awake()
    {
        // Use Awake instead of Start so it's ready before any trigger fires
        _parentLogic = GetComponentInParent<EnemyAI>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Pipe")) return;

        PipeLogic pipe = other.GetComponentInParent<PipeLogic>();
        if (pipe != null && _parentLogic != null)
            StartCoroutine(WaitToReact(pipe));
    }

    private IEnumerator WaitToReact(PipeLogic pipe)
    {
        // Clamp speed so division is safe; scale reaction time inversely with speed
        float speed = Mathf.Max(pipe.rotationSpeed, 1f);
        float reactionTime = Mathf.Clamp(baseReactionTime * (50f / speed), 0.05f, baseReactionTime);

        yield return new WaitForSeconds(reactionTime);

        // Guard: parent may have been destroyed during the wait
        if (_parentLogic != null)
            _parentLogic.DecideAction();
    }
}