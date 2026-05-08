using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager instance;

    // ─── Enemy Setup ──────────────────────────────────────────────────────────
    [Header("Enemy Setup")]
    public GameObject standardEnemyPrefab;
    public Transform[] spawnPoints;

    // ─── Spawn Settings ───────────────────────────────────────────────────────
    [Header("Spawn Settings")]
    [Tooltip("Max enemies alive at once.")]
    public int maxEnemiesAlive = 3;

    [Tooltip("Seconds between spawns at difficulty 0.")]
    public float baseSpawnInterval = 3f;

    [Tooltip("Seconds between spawns at difficulty 1 (max).")]
    public float minSpawnInterval = 0.8f;

    // ─── Enemy Scaling ────────────────────────────────────────────────────────
    [Header("Enemy Scaling")]
    public int baseEnemyHealth = 3;
    [Tooltip("Extra HP added per unit of DifficultyNormalized (0→1).")]
    public int healthScaleBonus = 4;

    // ─── State ────────────────────────────────────────────────────────────────
    private readonly List<GameObject> _activeEnemies = new List<GameObject>();
    private Coroutine _spawnLoop;
    private bool _running;

    private const float OccupiedDistanceSqr = 1.5f * 1.5f;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    // ─── Public API ───────────────────────────────────────────────────────────

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

    // Called by EnemyAI when health reaches 0
    public void OnEnemyDied(GameObject enemy)
    {
        _activeEnemies.Remove(enemy);
        Destroy(enemy);

        // Bonus score for kill
        GameManager.instance.AddBonusScore(5);
    }

    // ─── Spawn Loop ───────────────────────────────────────────────────────────

    // Continuously spawns enemies on an interval that shrinks with difficulty.
    // Respects maxEnemiesAlive as a hard cap — waits if the arena is full.
    private IEnumerator SpawnLoop()
    {
        while (_running)
        {
            // Wait for a slot to open if we're at cap
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

    // ─── Spawning ─────────────────────────────────────────────────────────────

    private void SpawnEnemy()
    {
        if (spawnPoints.Length == 0)
        {
            Debug.LogWarning("[SpawnManager] No spawn points assigned.");
            return;
        }

        Transform point = GetFreeSpawnPoint();
        if (point == null) return;

        GameObject enemy = Instantiate(standardEnemyPrefab, point.position, point.rotation);
        EnemyAI ai = enemy.GetComponent<EnemyAI>();

        if (ai != null)
        {
            // Scale health with difficulty
            float d = GameManager.instance.DifficultyNormalized;
            ai.health = baseEnemyHealth + Mathf.RoundToInt(healthScaleBonus * d);
            ai.isBoss = false;
        }

        _activeEnemies.Add(enemy);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private int ActiveEnemyCount()
    {
        // Prune destroyed entries while counting
        for (int i = _activeEnemies.Count - 1; i >= 0; i--)
            if (_activeEnemies[i] == null) _activeEnemies.RemoveAt(i);
        return _activeEnemies.Count;
    }

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

        return spawnPoints[startIndex];
    }

    private void ClearActiveEnemies()
    {
        foreach (GameObject e in _activeEnemies)
            if (e != null) Destroy(e);
        _activeEnemies.Clear();
    }
}