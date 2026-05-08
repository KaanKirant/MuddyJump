using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager instance;

    [Header("Enemy Setup")]
    public GameObject standardEnemyPrefab;
    public GameObject bossEnemyPrefab;
    public Transform[] spawnPoints;

    [Header("Spawn Settings")]
    public int maxEnemiesAlive = 3;
    public int killsToAdvanceFloor = 6;
    public float respawnDelay = 1.2f;

    private int _currentFloor;
    private int _killsThisFloor;
    private bool _floorClearing;

    private readonly List<GameObject> _activeEnemies = new List<GameObject>();

    // Occupied-radius check uses sqrMagnitude — avoids sqrt per frame
    private const float OccupiedDistanceSqr = 1.5f * 1.5f;

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    // Called by GameManager when a new floor begins
    public void StartFloor(int floor)
    {
        _currentFloor = floor;
        _killsThisFloor = 0;
        _floorClearing = false;

        ClearActiveEnemies();

        for (int i = 0; i < maxEnemiesAlive; i++)
            SpawnEnemy();
    }

    // Called by EnemyAI when health hits 0
    public void OnEnemyDied(GameObject enemy)
    {
        _activeEnemies.Remove(enemy);
        Destroy(enemy);
        _killsThisFloor++;

        if (_killsThisFloor >= KillsToAdvance())
        {
            if (_floorClearing) return;
            _floorClearing = true;
            ClearActiveEnemies();
            GameManager.instance.NextFloor();
            return;
        }

        if (!_floorClearing)
            StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);
        if (!_floorClearing)
            SpawnEnemy();
    }

    private void SpawnEnemy()
    {
        if (spawnPoints.Length == 0)
        {
            Debug.LogWarning("[SpawnManager] No spawn points assigned.");
            return;
        }

        Transform point = GetFreeSpawnPoint();
        if (point == null) return;

        bool isBossFloor = _currentFloor % 10 == 0 && _currentFloor > 0;
        GameObject prefab = isBossFloor && bossEnemyPrefab != null
            ? bossEnemyPrefab
            : standardEnemyPrefab;

        GameObject enemy = Instantiate(prefab, point.position, point.rotation);
        EnemyAI ai = enemy.GetComponent<EnemyAI>();

        if (ai != null)
        {
            ai.health = isBossFloor ? 6 + _currentFloor / 2 : 3 + _currentFloor / 3;
            ai.currentFloor = _currentFloor;
            ai.isBoss = isBossFloor;
        }

        _activeEnemies.Add(enemy);
    }

    private int KillsToAdvance() => killsToAdvanceFloor + _currentFloor;

    private Transform GetFreeSpawnPoint()
    {
        int startIndex = Random.Range(0, spawnPoints.Length);

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            Transform point = spawnPoints[(startIndex + i) % spawnPoints.Length];
            bool occupied = false;

            // Iterate backwards so we can safely prune null entries in-place
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

        // All points occupied — fall back to the random start point
        return spawnPoints[startIndex];
    }

    private void ClearActiveEnemies()
    {
        foreach (GameObject enemy in _activeEnemies)
            if (enemy != null) Destroy(enemy);
        _activeEnemies.Clear();
    }
}