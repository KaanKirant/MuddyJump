using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages enemy spawning via a continuous interval loop.
/// No floor/wave concept — spawning is purely time and difficulty driven.
///
/// Spawn interval shrinks from baseSpawnInterval → minSpawnInterval as
/// DifficultyNormalized goes 0→1. Enemy health scales the same way.
///
/// maxEnemiesAlive is a hard cap — the loop waits for a slot to open
/// before spawning the next enemy.
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
    [Tooltip("Spawn interval in seconds at difficulty 1 (max speed).")]
    public float minSpawnInterval = 0.8f;

    // ─── Enemy Health Scaling ─────────────────────────────────────────────────
    [Header("Enemy Scaling")]
    public int baseEnemyHealth = 3;
    [Tooltip("Max bonus HP added at full difficulty. Scales linearly from 0→this value.")]
    public int healthScaleBonus = 4;

    // ─── Private ──────────────────────────────────────────────────────────────
    private readonly List<GameObject> _activeEnemies = new List<GameObject>();
    private Coroutine _spawnLoop;
    private bool _running;

    // Squared distance threshold for "spawn point is occupied" check
    // avoids sqrt per frame in GetFreeSpawnPoint
    private const float OccupiedDistanceSqr = 1.5f * 1.5f;

    #region Unity Lifecycle

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    #endregion

    #region Public API

    /// <summary>Starts the spawn loop. Called by GameManager.Start().</summary>
    public void StartSpawning()
    {
        _running = true;
        if (_spawnLoop != null) StopCoroutine(_spawnLoop);
        _spawnLoop = StartCoroutine(SpawnLoop());
    }

    /// <summary>Stops spawning and destroys all active enemies. Called by GameManager.EndGame().</summary>
    public void StopSpawning()
    {
        _running = false;
        if (_spawnLoop != null) { StopCoroutine(_spawnLoop); _spawnLoop = null; }
        ClearActiveEnemies();
    }

    /// <summary>
    /// Called by EnemyAI.Die() when an enemy's health hits 0.
    /// Removes from tracking list, destroys the GameObject, and adds bonus score.
    /// Note: Destroy is called here — EnemyAI.Die() must NOT also call Destroy.
    /// </summary>
    public void OnEnemyDied(GameObject enemy)
    {
        _activeEnemies.Remove(enemy);
        Destroy(enemy);
        GameManager.instance.AddBonusScore(5); // +5 score per kill
    }

    #endregion

    #region Spawn Loop

    private IEnumerator SpawnLoop()
    {
        while (_running)
        {
            // Block until a slot is free — preserves maxEnemiesAlive cap
            yield return new WaitUntil(() => ActiveEnemyCount() < maxEnemiesAlive || !_running);

            if (!_running) yield break;

            SpawnEnemy();

            // Interval shrinks with difficulty — starts slow, ramps to aggressive
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
        if (spawnPoints.Length == 0)
        {
            Debug.LogWarning("[SpawnManager] Missing spawn points");
            return;
        }

        Transform point = GetFreeSpawnPoint();
        if (point == null) return;

        GameObject selectedPrefab = null;
        bool isBoss = false;

        if (spawnConfigs.Count > 0)
        {
            // Filter configs by difficulty threshold and build weighted list
            List<EnemySpawnConfig> validConfigs = new List<EnemySpawnConfig>();
            float currentDifficulty = GameManager.instance != null
                ? GameManager.instance.DifficultyNormalized
                : 0f;

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

            // Weighted random selection
            EnemySpawnConfig selected = SelectWeightedConfig(validConfigs);
            if (selected == null) return;

            selectedPrefab = selected.enemyPrefab;
            isBoss = selected.isBoss;
        }
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

    /// <summary>
    /// Selects a random EnemySpawnConfig from the list weighted by spawnWeight.
    /// Uses cumulative probability method for efficient weighted selection.
    /// </summary>
    private EnemySpawnConfig SelectWeightedConfig(List<EnemySpawnConfig> configs)
    {
        if (configs.Count == 0) return null;

        // Calculate total weight
        float totalWeight = 0f;
        foreach (var config in configs)
            totalWeight += config.spawnWeight;

        if (totalWeight <= 0) return null;

        // Pick random point in weight range
        float pick = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        // Return config at that point
        foreach (var config in configs)
        {
            cumulative += config.spawnWeight;
            if (pick <= cumulative) return config;
        }

        return configs[configs.Count - 1]; // Fallback to last (should not reach)
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Returns live enemy count, pruning any null entries (destroyed mid-loop).
    /// </summary>
    private int ActiveEnemyCount()
    {
        for (int i = _activeEnemies.Count - 1; i >= 0; i--)
            if (_activeEnemies[i] == null) _activeEnemies.RemoveAt(i);
        return _activeEnemies.Count;
    }

    /// <summary>
    /// Finds a spawn point not already occupied by an active enemy.
    /// Starts at a random index to avoid always using the same points first.
    /// Falls back to the random start point if all are occupied.
    /// </summary>
    private Transform GetFreeSpawnPoint()
    {
        int startIndex = Random.Range(0, spawnPoints.Length);

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            Transform point = spawnPoints[(startIndex + i) % spawnPoints.Length];
            bool occupied = false;

            for (int j = _activeEnemies.Count - 1; j >= 0; j--)
            {
                if (_activeEnemies[j] == null) { _activeEnemies.RemoveAt(j); continue; }
                if ((_activeEnemies[j].transform.position - point.position).sqrMagnitude < OccupiedDistanceSqr)
                {
                    occupied = true;
                    break;
                }
            }

            if (!occupied) return point;
        }

        return spawnPoints[startIndex]; // All occupied — fallback
    }

    private void ClearActiveEnemies()
    {
        foreach (GameObject e in _activeEnemies)
            if (e != null) Destroy(e);
        _activeEnemies.Clear();
    }

    #endregion
}