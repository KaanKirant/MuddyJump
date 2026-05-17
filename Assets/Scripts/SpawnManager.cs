using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages enemy spawning via a continuous interval loop.
///
/// Fix — spawn cooldown not working:
///   Old order: wait for slot → spawn → wait interval
///   The wait-for-slot unblocked the moment an enemy died, so the spawn
///   happened instantly and the interval only applied to the NEXT cycle.
///
///   New order: wait interval → wait for slot → spawn
///   Every spawn now always has a cooldown before it, regardless of
///   whether it was triggered by a death or by the loop cycling normally.
/// </summary>
public class SpawnManager : MonoBehaviour
{
    public static SpawnManager instance;

    // ─── Enemy Setup ──────────────────────────────────────────────────────────
    [Header("Enemy Setup")]
    [Tooltip("Spawn configurations for each enemy type.")]
    public List<EnemySpawnConfig> spawnConfigs = new List<EnemySpawnConfig>();
    [Tooltip("Transforms representing spawn points in the scene.")]
    public Transform[] spawnPoints;

    // ─── Spawn Settings ───────────────────────────────────────────────────────
    [Header("Spawn Settings")]
    [Tooltip("Maximum enemies alive simultaneously.")]
    public int maxEnemiesAlive = 3;
    [Tooltip("Spawn interval in seconds at difficulty 0 (game start).")]
    public float baseSpawnInterval = 3f;
    [Tooltip("Minimum spawn interval in seconds at difficulty 1 (max).")]
    public float minSpawnInterval = 0.8f;

    // ─── Enemy Health Scaling ─────────────────────────────────────────────────
    [Header("Enemy Scaling")]
    public int baseEnemyHealth = 3;
    [Tooltip("Max bonus HP added at full difficulty.")]
    public int healthScaleBonus = 4;

    // ─── Spawn Overlap ────────────────────────────────────────────────────────
    [Header("Spawn Overlap")]
    [Tooltip("Minimum XZ distance between a spawn point and any living enemy.")]
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
            // ── Wait the interval FIRST ──────────────────────────────────────
            // This is the fix. Previously the interval came AFTER spawning,
            // so a death would instantly unblock WaitUntil and spawn with
            // zero delay. Now every spawn — initial or replacement — always
            // waits a full interval before it can happen.
            float interval = Mathf.Lerp(
                baseSpawnInterval,
                minSpawnInterval,
                GameManager.instance.DifficultyNormalized
            );
            yield return new WaitForSeconds(interval);

            if (!_running) yield break;

            // ── Then wait for a free slot ────────────────────────────────────
            // If we're already under the cap this resolves immediately.
            // If we're at the cap we wait here until a slot opens.
            yield return new WaitUntil(() => ActiveEnemyCount() < maxEnemiesAlive || !_running);

            if (!_running) yield break;

            SpawnEnemy();
        }
    }

    #endregion

    #region Spawning

    private void SpawnEnemy()
    {
        if (spawnPoints.Length == 0)
        {
            Debug.LogWarning("[SpawnManager] No spawn points assigned.");
            return;
        }

        Transform point = GetFreeSpawnPoint();
        if (point == null) return;

        GameObject selectedPrefab = null;
        bool isBoss = false;

        if (spawnConfigs.Count > 0)
        {
            float currentDifficulty = GameManager.instance != null
                ? GameManager.instance.DifficultyNormalized
                : 0f;

            List<EnemySpawnConfig> validConfigs = new List<EnemySpawnConfig>();
            foreach (var config in spawnConfigs)
            {
                if (config.IsValidForSpawning() && currentDifficulty >= config.difficultyThreshold)
                    validConfigs.Add(config);
            }

            if (validConfigs.Count == 0)
            {
                Debug.LogWarning("[SpawnManager] No valid spawn configs for current difficulty.");
                return;
            }

            EnemySpawnConfig selected = SelectWeightedConfig(validConfigs);
            if (selected == null) return;

            selectedPrefab = selected.enemyPrefab;
            isBoss = selected.isBoss;
        }

        if (selectedPrefab == null) return;

        GameObject enemy = Instantiate(selectedPrefab, point.position, point.rotation);
        EnemyAI ai = enemy.GetComponent<EnemyAI>();

        if (ai != null)
        {
            float d = GameManager.instance.DifficultyNormalized;
            ai.Health = baseEnemyHealth + Mathf.RoundToInt(healthScaleBonus * d);
            ai.MaxHealth = ai.Health;
            ai.isBoss = isBoss;
        }

        _activeEnemies.Add(enemy);
    }

    private EnemySpawnConfig SelectWeightedConfig(List<EnemySpawnConfig> configs)
    {
        if (configs.Count == 0) return null;

        float totalWeight = 0f;
        foreach (var config in configs) totalWeight += config.spawnWeight;
        if (totalWeight <= 0f) return null;

        float pick = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (var config in configs)
        {
            cumulative += config.spawnWeight;
            if (pick <= cumulative) return config;
        }

        return configs[configs.Count - 1];
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
    /// XZ-only distance check — Y is excluded because on a fixed platform
    /// enemies may be at slightly different heights after jumping.
    /// Returns null if all points are occupied (spawn skipped this tick).
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

        Debug.LogWarning("[SpawnManager] All spawn points occupied — skipping spawn.");
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