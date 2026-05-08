using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager instance;

    [Header("Enemy Setup")]
    public GameObject standardEnemyPrefab;
    public GameObject bossEnemyPrefab;      // Assign a prefab with EnemyAI isBoss = true
    public Transform[] spawnPoints;

    [Header("Spawn Settings")]
    public int maxEnemiesAlive = 3;         // Always keep this many enemies on the floor
    public int killsToAdvanceFloor = 6;     // Total kills needed to trigger floor clear
    public float respawnDelay = 1.2f;       // Delay before a replacement spawns after a kill

    private int currentFloor = 0;
    private int killsThisFloor = 0;
    private List<GameObject> activeEnemies = new List<GameObject>();
    private bool floorClearing = false;     // Prevents new spawns during the transition

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    // Called by GameManager on each new floor
    public void StartFloor(int floor)
    {
        currentFloor = floor;
        killsThisFloor = 0;
        floorClearing = false;
        ClearActiveEnemies();

        // Fill all slots immediately at floor start
        for (int i = 0; i < maxEnemiesAlive; i++)
            SpawnEnemy();
    }

    // Called by EnemyAI.TakeDamage when health hits 0
    public void OnEnemyDied(GameObject enemy)
    {
        activeEnemies.Remove(enemy);
        Destroy(enemy);
        killsThisFloor++;

        if (killsThisFloor >= killsToAdvance())
        {
            if (!floorClearing)
            {
                floorClearing = true;
                ClearActiveEnemies();
                GameManager.instance.NextFloor();
            }
            return;
        }

        // Floor not done — fill the empty slot after a short delay
        if (!floorClearing)
            StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);
        if (!floorClearing)
            SpawnEnemy();
    }

    private void SpawnEnemy()
    {
        if (spawnPoints.Length == 0)
        {
            Debug.LogWarning("SpawnManager: No spawn points assigned.");
            return;
        }

        // Pick a spawn point that doesn't already have an enemy on it
        Transform point = GetFreeSpawnPoint();
        if (point == null) return;

        bool isBossFloor = currentFloor % 10 == 0 && currentFloor > 0;
        GameObject prefab = (isBossFloor && bossEnemyPrefab != null)
            ? bossEnemyPrefab
            : standardEnemyPrefab;

        GameObject enemy = Instantiate(prefab, point.position, point.rotation);
        EnemyAI ai = enemy.GetComponent<EnemyAI>();

        if (ai != null)
        {
            // Scale health with floor — boss gets extra
            ai.health = isBossFloor
                ? 6 + currentFloor / 2
                : 3 + currentFloor / 3;

            ai.currentFloor = currentFloor;
            ai.isBoss = isBossFloor;
        }

        activeEnemies.Add(enemy);
    }

    // Kills required scales with floor so early floors feel easy
    private int killsToAdvance()
    {
        return killsToAdvanceFloor + currentFloor;
    }

    private Transform GetFreeSpawnPoint()
    {
        const float occupiedDistance = 1.5f;
        float occupiedDistanceSqr = occupiedDistance * occupiedDistance;

        int startIndex = Random.Range(0, spawnPoints.Length);

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            Transform point = spawnPoints[(startIndex + i) % spawnPoints.Length];

            bool occupied = false;

            for (int j = activeEnemies.Count - 1; j >= 0; j--)
            {
                GameObject enemy = activeEnemies[j];

                if (enemy == null)
                {
                    activeEnemies.RemoveAt(j);
                    continue;
                }

                if ((enemy.transform.position - point.position).sqrMagnitude < occupiedDistanceSqr)
                {
                    occupied = true;
                    break;
                }
            }

            if (!occupied)
                return point;
        }

        return spawnPoints[startIndex];
    }

    private void ClearActiveEnemies()
    {
        foreach (GameObject enemy in activeEnemies)
            if (enemy != null) Destroy(enemy);
        activeEnemies.Clear();
    }

    private Transform[] ShuffledSpawnPoints()
    {
        Transform[] copy = (Transform[])spawnPoints.Clone();
        for (int i = copy.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (copy[i], copy[j]) = (copy[j], copy[i]);
        }
        return copy;
    }
}