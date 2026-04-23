using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager instance;

    [Header("Enemy Setup")]
    public GameObject[] enemyPrefabs;       // Drag your enemy prefabs here in the Inspector
    public Transform[] spawnPoints;         // Drag your spawn point transforms here

    [Header("Spawn Settings")]
    public int enemiesPerFloor = 1;         // How many enemies to spawn when a floor starts
    public float delayBetweenSpawns = 0.5f; // Seconds between each spawn when spawning multiple

    // Tracks all enemies alive in the current floor
    private List<GameObject> activeEnemies = new List<GameObject>();

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    // Call this from GameManager when a new floor begins
    public void SpawnFloor(int floorNumber)
    {
        ClearActiveEnemies();
        StartCoroutine(SpawnSequence(floorNumber));
    }

    private IEnumerator SpawnSequence(int floorNumber)
    {
        // Scale enemy count with floor number — optional, remove if you want fixed count
        int count = Mathf.Min(enemiesPerFloor + (floorNumber / 3), spawnPoints.Length);

        // Shuffle spawn points so enemies don't always appear in the same spots
        Transform[] shuffled = ShuffledSpawnPoints();

        for (int i = 0; i < count; i++)
        {
            SpawnEnemy(shuffled[i], floorNumber);

            if (i < count - 1)
                yield return new WaitForSeconds(delayBetweenSpawns);
        }
    }

    private void SpawnEnemy(Transform spawnPoint, int floorNumber)
    {
        if (enemyPrefabs.Length == 0 || spawnPoints.Length == 0)
        {
            Debug.LogWarning("SpawnManager: No enemy prefabs or spawn points assigned.");
            return;
        }

        // Pick a random prefab from the array
        GameObject prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        GameObject enemy = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);

        // Scale enemy health with floor number
        EnemyAI ai = enemy.GetComponent<EnemyAI>();
        if (ai != null)
            ai.health = 3 + floorNumber; // Floor 1 = 4hp, floor 5 = 8hp, etc.

        activeEnemies.Add(enemy);
    }

    // Call this from EnemyAI when an enemy dies
    public void OnEnemyDied(GameObject enemy)
    {
        activeEnemies.Remove(enemy);
        Destroy(enemy);

        if (activeEnemies.Count == 0)
        {
            Debug.Log("All enemies defeated — floor clear!");
            GameManager.instance.NextFloor();
        }
    }

    // Destroys any leftover enemies from a previous floor before spawning new ones
    private void ClearActiveEnemies()
    {
        foreach (GameObject enemy in activeEnemies)
        {
            if (enemy != null)
                Destroy(enemy);
        }
        activeEnemies.Clear();
    }

    // Fisher-Yates shuffle on a copy of the spawnPoints array
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