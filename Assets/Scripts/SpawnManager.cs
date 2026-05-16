using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages enemy spawning via a continuous interval loop.
///
/// Overlap fix: GetFreeSpawnPoint compares XZ distance only.
/// The platform rises continuously so enemy world-Y drifts away from
/// spawn point world-Y every frame — a 3D distance check always reads
/// "far apart" and lets enemies stack on the same XZ spot. Stripping Y
/// before the check correctly detects same-tile occupancy.
///
/// Supports multiple enemy prefabs via EnemySpawnEntry array.
/// </summary>
public class SpawnManager : MonoBehaviour
{
    public static SpawnManager instance;

    // ─── Enemy Setup ──────────────────────────────────────────────────────────
    [Header("Enemy Setup")]
    [Tooltip("Enemy variants. Each entry has a prefab, spawn weight, and difficulty threshold.")]
    public EnemySpawnEntry[] enemyPrefabs;
    public Transform[] spawnPoints;

    // ─── Spawn Settings ───────────────────────────────────────────────────────
    [Header("Spawn Settings")]
    public int maxEnemiesAlive = 3;
    public float baseSpawnInterval = 3f;
    public float minSpawnInterval = 0.8f;

    // ─── Enemy Health Scaling ─────────────────────────────────────────────────
    [Header("Enemy Scaling")]
    public int baseEnemyHealth = 3;
    public int healthScaleBonus = 4;

    // ─── Spawn Overlap ────────────────────────────────────────────────────────
    [Header("Spawn Overlap")]
    [Tooltip("Minimum XZ distance between a spawn point and any living enemy. " +
             "Should be >= your character capsule diameter. Increase if enemies still stack.")]
    public float occupiedRadius = 2.5f;

    // ─── Private ──────────────────────────────────────────────────────────────
    private readonly List<GameObject> _activeEnemies = new List<GameObject>();
    private Coroutine _spawnLoop;
    private bool _running;

    #region Unity Lifecycle

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    #endregion

    #region Public API

    public void StartSpawning()
    {
        _running = true;
        if (_spawnLoop != null) StopCoroutine(_spawnLoop);
        _spawnLoop = StartCoroutine(SpawnLoop());
    }

    public void StopSpawning()
    {
        _running = false;
        if (_spawnLoop != null) { StopCoroutine(_spawnLoop); _spawnLoop = null; }
        ClearActiveEnemies();
    }

    public void OnEnemyDied(GameObject enemy)
    {
        _activeEnemies.Remove(enemy);
        Destroy(enemy);
        GameManager.instance.AddBonusScore(5);
    }

    #endregion

    #region Spawn Loop

    private IEnumerator SpawnLoop()
    {
        while (_running)
        {
            yield return new WaitUntil(() => ActiveEnemyCount() < maxEnemiesAlive || !_running);

            if (!_running) yield break;

            SpawnEnemy();

            float interval = Mathf.Lerp(
                baseSpawnInterval,
                minSpawnInterval,
                GameManager.instance.DifficultyNormalized
            );
            yield return new WaitForSeconds(interval);
        }
    }

    #endregion

    #region Spawning

    private void SpawnEnemy()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
        {
            Debug.LogWarning("[SpawnManager] No enemy prefabs assigned.");
            return;
        }

        if (spawnPoints.Length == 0)
        {
            Debug.LogWarning("[SpawnManager] No spawn points assigned.");
            return;
        }

        Transform point = GetFreeSpawnPoint();
        if (point == null) return;   // All points occupied — skip this tick

        GameObject prefab = PickEnemyPrefab();
        if (prefab == null) return;

        GameObject enemy = Instantiate(prefab, point.position, point.rotation);
        EnemyAI ai = enemy.GetComponent<EnemyAI>();

        if (ai != null)
        {
            float d = GameManager.instance.DifficultyNormalized;
            ai.Health = baseEnemyHealth + Mathf.RoundToInt(healthScaleBonus * d);
            ai.MaxHealth = ai.Health;
            ai.isBoss = false;
        }

        _activeEnemies.Add(enemy);
    }

    /// <summary>
    /// Weighted random selection from currently eligible prefabs.
    /// A prefab is eligible when DifficultyNormalized >= unlockAtDifficulty.
    /// Falls back to entry[0] if nothing else qualifies.
    /// </summary>
    private GameObject PickEnemyPrefab()
    {
        float difficulty = GameManager.instance.DifficultyNormalized;
        float totalWeight = 0f;

        for (int i = 0; i < enemyPrefabs.Length; i++)
        {
            if (enemyPrefabs[i].prefab != null && difficulty >= enemyPrefabs[i].unlockAtDifficulty)
                totalWeight += enemyPrefabs[i].weight;
        }

        if (totalWeight <= 0f)
            return enemyPrefabs[0].prefab;

        float roll = Random.value * totalWeight;
        float running = 0f;

        for (int i = 0; i < enemyPrefabs.Length; i++)
        {
            if (enemyPrefabs[i].prefab == null) continue;
            if (difficulty < enemyPrefabs[i].unlockAtDifficulty) continue;

            running += enemyPrefabs[i].weight;
            if (roll <= running)
                return enemyPrefabs[i].prefab;
        }

        return enemyPrefabs[0].prefab;
    }

    #endregion

    #region Helpers

    private int ActiveEnemyCount()
    {
        for (int i = _activeEnemies.Count - 1; i >= 0; i--)
            if (_activeEnemies[i] == null) _activeEnemies.RemoveAt(i);
        return _activeEnemies.Count;
    }

    /// <summary>
    /// Returns a spawn point whose XZ position is at least occupiedRadius away
    /// from every living enemy. Y is excluded — the platform rises continuously
    /// so enemy world-Y is always higher than the spawn point's world-Y, making
    /// a 3D distance check incorrectly pass and allowing XZ stacking.
    /// Returns null if all points are occupied (spawn is skipped for this tick).
    /// </summary>
    private Transform GetFreeSpawnPoint()
    {
        float radiusSqr = occupiedRadius * occupiedRadius;
        int startIndex = Random.Range(0, spawnPoints.Length);

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            Transform point = spawnPoints[(startIndex + i) % spawnPoints.Length];
            bool occupied = false;

            for (int j = _activeEnemies.Count - 1; j >= 0; j--)
            {
                if (_activeEnemies[j] == null) { _activeEnemies.RemoveAt(j); continue; }

                // Flatten both positions to XZ before comparing
                Vector3 enemyXZ = _activeEnemies[j].transform.position; enemyXZ.y = 0f;
                Vector3 pointXZ = point.position; pointXZ.y = 0f;

                if ((enemyXZ - pointXZ).sqrMagnitude < radiusSqr)
                {
                    occupied = true;
                    break;
                }
            }

            if (!occupied) return point;
        }

        Debug.LogWarning("[SpawnManager] All spawn points occupied — skipping spawn this tick.");
        return null;
    }

    private void ClearActiveEnemies()
    {
        foreach (GameObject e in _activeEnemies)
            if (e != null) Destroy(e);
        _activeEnemies.Clear();
    }

    #endregion
}

/// <summary>One enemy variant entry in the SpawnManager pool.</summary>
[System.Serializable]
public class EnemySpawnEntry
{
    [Tooltip("Enemy prefab to spawn.")]
    public GameObject prefab;

    [Tooltip("Relative spawn frequency. Higher = spawns more often among eligible entries.")]
    public float weight = 1f;

    [Tooltip("Only eligible when DifficultyNormalized >= this value. 0 = always available.")]
    [Range(0f, 1f)]
    public float unlockAtDifficulty = 0f;
}