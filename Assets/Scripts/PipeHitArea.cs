using UnityEngine;

public class PipeHitArea : MonoBehaviour
{
    private PipeLogic parentLogic;
    void Start()
    {
        parentLogic = GetComponentInParent<PipeLogic>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player") || other.gameObject.CompareTag("Enemy"))
        {
            if (parentLogic != null)
            {
                parentLogic.isHitArea = true;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player") || other.gameObject.CompareTag("Enemy"))
        {
            if (parentLogic != null)
            {
                parentLogic.isHitArea = false;
            }
        }
    }
}