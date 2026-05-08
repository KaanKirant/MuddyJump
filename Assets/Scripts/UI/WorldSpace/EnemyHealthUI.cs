using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Slider healthSlider;

    [Header("Follow Settings")]
    [SerializeField] private Transform target;

    [SerializeField]
    private Vector3 worldOffset =
        new Vector3(0f, 2f, 0f);

    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        transform.position =
            target.position + worldOffset;

        if (mainCamera != null)
        {
            transform.forward =
                mainCamera.transform.forward;
        }
    }

    public void Initialize(Transform followTarget, float maxHealth)
    {
        target = followTarget;

        healthSlider.maxValue = maxHealth;
        healthSlider.value = maxHealth;
    }

    public void UpdateHealth(float currentHealth)
    {
        healthSlider.value = currentHealth;
    }
}