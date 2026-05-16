using UnityEngine;

/// <summary>
/// Scriptable Object defining a single enemy spawn configuration.
/// Used by SpawnManager to support weighted, difficulty-based enemy spawning.
///
/// Usage:
///   1. Create a new ScriptableObject asset (Right-click > Create > Enemy Spawn Config)
///   2. Assign enemy prefab, weight, and difficulty threshold
///   3. Add to SpawnManager's spawnConfigs list
/// </summary>
[CreateAssetMenu(fileName = "EnemySpawnConfig", menuName = "Gameplay/Enemy Spawn Config")]
public class EnemySpawnConfig : ScriptableObject
{
    [Tooltip("Enemy prefab to spawn. Must have EnemyAI component.")]
    public GameObject enemyPrefab;

    [Range(0.1f, 10f)]
    [Tooltip("Relative spawn weight. Higher = spawns more frequently. Example: weight 2.0 spawns twice as often as weight 1.0.")]
    public float spawnWeight = 1f;

    [Range(0f, 1f)]
    [Tooltip("Only spawn this enemy when difficulty >= this threshold. 0 = always, 1 = only at max difficulty.")]
    public float difficultyThreshold = 0f;

    [Tooltip("If true, this enemy type is marked as a boss with special behavior bonuses.")]
    public bool isBoss = false;

    [TextArea(2, 4)]
    [Tooltip("Description for editor reference only — helps organize spawn configs.")]
    public string description = "New enemy spawn configuration";

    public bool IsValidForSpawning()
    {
        return enemyPrefab != null && spawnWeight > 0f;
    }
}