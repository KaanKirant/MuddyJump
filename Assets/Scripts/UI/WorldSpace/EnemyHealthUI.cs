using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space health bar that follows an enemy transform.
/// Spawned and destroyed by EnemyAI — not placed in the scene directly.
///
/// Attach this to a World Space Canvas prefab sized to the enemy.
/// The canvas should have:
///   - Render Mode: World Space
///   - A row of Image components assigned to heartImages
///
/// EnemyAI.SpawnHealthUI() calls Initialize() after instantiation.
/// EnemyAI.TakeDamage() calls UpdateHealth() on each hit.
/// EnemyAI.OnDestroy() destroys this GameObject.
/// </summary>
public class EnemyHealthUI : MonoBehaviour
{
    [Header("Heart Images")]
    [Tooltip("One Image per health point. Sized to match maxHealth in SpawnManager.")]
    [SerializeField] private Image[] heartImages;
    [SerializeField] private Sprite fullHeart;
    [SerializeField] private Sprite emptyHeart;

    [Header("Follow Settings")]
    [Tooltip("Offset above the enemy pivot in world space.")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2f, 0f);

    private Transform _target;
    private Camera _mainCamera;
    private int _maxHealth;

    #region Unity Lifecycle

    private void Awake()
    {
        _mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (_target == null) return;

        // Follow enemy position
        transform.position = _target.position + worldOffset;

        // Always face the camera — billboard behaviour
        if (_mainCamera != null)
            transform.forward = _mainCamera.transform.forward;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Called by EnemyAI.SpawnHealthUI() immediately after Instantiate.
    /// maxHealth is int — matches EnemyAI.maxHealth.
    /// </summary>
    public void Initialize(Transform followTarget, int maxHealth)
    {
        _target = followTarget;
        _maxHealth = maxHealth;
        UpdateHealth(maxHealth);
    }

    /// <summary>Called by EnemyAI.TakeDamage() after health is reduced.</summary>
    public void UpdateHealth(int currentHealth)
    {
        for (int i = 0; i < heartImages.Length; i++)
        {
            if (heartImages[i] == null) continue;

            // Slots beyond maxHealth are hidden — handles variable enemy health
            if (i < _maxHealth)
            {
                heartImages[i].enabled = true;
                heartImages[i].sprite = i < currentHealth ? fullHeart : emptyHeart;
            }
            else
            {
                heartImages[i].enabled = false;
            }
        }
    }

    #endregion
}