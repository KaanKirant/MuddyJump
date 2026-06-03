using UnityEngine;

/// <summary>
/// Scriptable Object defining a single consumable spawn configuration.
/// Used by ConsumableSpawnManager to support weighted consumable spawning.
///
/// Usage:
///   1. Create a new ScriptableObject asset (Right-click > Create > Consumable Spawn Config)
///   2. Assign consumable prefab and spawn weight
///   3. Add to ConsumableSpawnManager's spawnConfigs list
/// </summary>
[CreateAssetMenu(fileName = "ConsumableSpawnConfig", menuName = "Gameplay/Consumable Spawn Config")]
public class ConsumableSpawnConfig : ScriptableObject
{
    [Tooltip("Consumable prefab to spawn. Must have ConsumableItem component.")]
    public GameObject consumablePrefab;

    [Range(0.1f, 10f)]
    [Tooltip("Relative spawn weight. Higher = spawns more frequently. Example: weight 2.0 spawns twice as often as weight 1.0.")]
    public float spawnWeight = 1f;

    [TextArea(2, 4)]
    [Tooltip("Description for editor reference only — helps organize spawn configs.")]
    public string description = "New consumable spawn configuration";

    public bool IsValidForSpawning()
    {
        return consumablePrefab != null && spawnWeight > 0f;
    }
}
