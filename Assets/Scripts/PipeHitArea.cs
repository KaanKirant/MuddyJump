using UnityEngine;

public class PipeHitArea : MonoBehaviour
{
    private PipeLogic parentLogic;

    private void Start()
    {
        parentLogic = GetComponentInParent<PipeLogic>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.CompareTag("Enemy"))
            parentLogic.isHitArea = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.CompareTag("Enemy"))
            parentLogic.isHitArea = false;
    }
}