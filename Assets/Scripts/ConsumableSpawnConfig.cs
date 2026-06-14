using UnityEngine;

/// <summary>
/// ScriptableObject defining one consumable spawn entry.
/// Used by ConsumableSpawnManager for weighted random selection.
///
/// Usage:
///   1. Right-click in Project → Create → Gameplay → Consumable Spawn Config
///   2. Assign the consumable prefab (must have a ConsumableItem component)
///   3. Set spawnWeight — higher weight = spawns more frequently relative to others
///   4. Add the asset to ConsumableSpawnManager.spawnConfigs in the scene
/// </summary>
[CreateAssetMenu(fileName = "ConsumableSpawnConfig", menuName = "Gameplay/Consumable Spawn Config")]
public class ConsumableSpawnConfig : ScriptableObject
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Tooltip("Consumable prefab to instantiate. Must have a ConsumableItem component.")]
    public GameObject consumablePrefab;

    [Range(0.1f, 10f)]
    [Tooltip("Relative spawn weight. Weight 2 spawns twice as often as weight 1.")]
    public float spawnWeight = 1f;

    [TextArea(2, 4)]
    [Tooltip("Editor-only description to help organise configs in large projects.")]
    public string description = "New consumable spawn configuration";

    // ─── Validation ───────────────────────────────────────────────────────────

    /// <summary>Returns true when this config has a valid prefab and a positive weight.</summary>
    public bool IsValidForSpawning() => consumablePrefab != null && spawnWeight > 0f;
}