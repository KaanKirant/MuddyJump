using UnityEngine;

/// <summary>
/// ScriptableObject defining one enemy variant's spawn parameters.
/// Used by SpawnManager for weighted, difficulty-gated enemy selection.
///
/// Usage:
///   1. Right-click in Project → Create → Gameplay → Enemy Spawn Config
///   2. Assign the enemy prefab (must have an EnemyAI component)
///   3. Set spawnWeight and difficultyThreshold
///   4. Add the asset to SpawnManager.spawnConfigs in the scene
/// </summary>
[CreateAssetMenu(fileName = "EnemySpawnConfig", menuName = "Gameplay/Enemy Spawn Config")]
public class EnemySpawnConfig : ScriptableObject
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Tooltip("Enemy prefab to instantiate. Must have an EnemyAI component.")]
    public GameObject enemyPrefab;

    [Range(0.1f, 10f)]
    [Tooltip("Relative spawn weight. Weight 2 spawns twice as often as weight 1.")]
    public float spawnWeight = 1f;

    [Range(0f, 1f)]
    [Tooltip("Minimum DifficultyNormalized required to include this config in the pool. " +
             "0 = available from the start. 1 = only at maximum difficulty.")]
    public float difficultyThreshold = 0f;

    [Tooltip("Marks the spawned enemy as a boss — EnemyAI applies bonus behaviour.")]
    public bool isBoss = false;

    [TextArea(2, 4)]
    [Tooltip("Editor-only description to help organise configs in large projects.")]
    public string description = "New enemy spawn configuration";

    // ─── Validation ───────────────────────────────────────────────────────────

    /// <summary>Returns true when the config has a valid prefab and a positive weight.</summary>
    public bool IsValidForSpawning() => enemyPrefab != null && spawnWeight > 0f;
}