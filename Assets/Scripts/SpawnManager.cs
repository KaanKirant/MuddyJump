using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages enemy spawning via a continuous interval loop.
///
/// Spawning is purely time- and difficulty-driven — no waves or floors.
/// The interval shrinks from baseSpawnInterval → minSpawnInterval as
/// DifficultyNormalized goes 0→1. Enemy health scales the same way.
///
/// maxEnemiesAlive is a hard cap. When the arena is full the loop waits
/// for a slot to open, then enforces a post-death cooldown before spawning
/// the replacement — every spawn always has a gap behind it.
///
/// Enemy variants are defined via EnemySpawnConfig ScriptableObjects.
/// Each config has a prefab, a spawn weight, and a difficulty threshold
/// so harder enemies unlock progressively as the game ramps up.
/// </summary>
public class SpawnManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static SpawnManager instance;

    // ─── Enemy Setup ──────────────────────────────────────────────────────────
    [Header("Enemy Setup")]
    [Tooltip("Spawn configurations for each enemy type. Create via Right-click > Gameplay > Enemy Spawn Config.")]
    public List<EnemySpawnConfig> spawnConfigs = new List<EnemySpawnConfig>();

    [Tooltip("World transforms representing valid spawn positions.")]
    public Transform[] spawnPoints;

    // ─── Spawn Settings ───────────────────────────────────────────────────────
    [Header("Spawn Settings")]
    [Tooltip("Maximum enemies alive simultaneously.")]
    public int maxEnemiesAlive = 3;

    [Tooltip("Seconds between spawns at difficulty 0.")]
    public float baseSpawnInterval = 3f;

    [Tooltip("Seconds between spawns at difficulty 1 (max). Also used as the death-replacement cooldown.")]
    public float minSpawnInterval = 0.8f;

    // ─── Enemy Health Scaling ─────────────────────────────────────────────────
    [Header("Enemy Scaling")]
    [Tooltip("Base HP assigned to every enemy at difficulty 0.")]
    public int baseEnemyHealth = 3;

    [Tooltip("Max bonus HP added at full difficulty. Scales linearly from 0 → this value.")]
    public int healthScaleBonus = 4;

    // ─── Spawn Overlap ────────────────────────────────────────────────────────
    [Header("Spawn Overlap")]
    [Tooltip("Minimum XZ distance between a spawn point and any living enemy. " +
             "Increase if enemies stack. Should be >= character capsule diameter.")]
    public float occupiedRadius = 2.5f;

    // ─── Private ──────────────────────────────────────────────────────────────
    private readonly List<GameObject> _activeEnemies = new List<GameObject>();

    // Reused each SpawnEnemy() call — avoids a new List allocation per spawn.
    private readonly List<EnemySpawnConfig> _validConfigsBuffer = new List<EnemySpawnConfig>();

    private Coroutine _spawnLoop;
    private Coroutine _deathCooldown;
    private bool _running;

    /// <summary>True while a post-death cooldown is blocking the spawn loop.</summary>
    private bool _deathCooldownActive;

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>Starts the spawn loop. Called by GameManager.Start().</summary>
    public void StartSpawning()
    {
        _running = true;
        if (_spawnLoop != null) StopCoroutine(_spawnLoop);
        _spawnLoop = StartCoroutine(SpawnLoop());
    }

    /// <summary>
    /// Stops spawning and destroys all active enemies.
    /// Called by GameManager.EndGame().
    /// </summary>
    public void StopSpawning()
    {
        _running = false;
        if (_spawnLoop != null) { StopCoroutine(_spawnLoop); _spawnLoop = null; }
        if (_deathCooldown != null) { StopCoroutine(_deathCooldown); _deathCooldown = null; }
        _deathCooldownActive = false;
        ClearActiveEnemies();
    }

    /// <summary>
    /// Called by EnemyAI.Die() when an enemy's health reaches 0.
    /// Removes the enemy from tracking, destroys it, awards bonus score,
    /// and starts a post-death cooldown so the replacement isn't instant.
    /// </summary>
    public void OnEnemyDied(GameObject enemy)
    {
        _activeEnemies.Remove(enemy);
        Destroy(enemy);
        GameManager.instance.AddBonusScore(5);

        if (!_running) return;

        // Restart the death cooldown — overlapping kills reset the timer.
        if (_deathCooldown != null) StopCoroutine(_deathCooldown);
        _deathCooldown = StartCoroutine(DeathCooldown());
    }

    // ─── Spawn Loop ───────────────────────────────────────────────────────────

    private IEnumerator SpawnLoop()
    {
        // Brief grace period before the first enemy — lets the player settle in.
        yield return new WaitForSeconds(baseSpawnInterval);

        while (_running)
        {
            // Block until a slot opens.
            yield return new WaitUntil(() => ActiveEnemyCount() < maxEnemiesAlive || !_running);
            if (!_running) yield break;

            // Block until the post-death cooldown clears.
            yield return new WaitUntil(() => !_deathCooldownActive || !_running);
            if (!_running) yield break;

            SpawnEnemy();

            // Interval shrinks as difficulty increases.
            float interval = Mathf.Lerp(
                baseSpawnInterval,
                minSpawnInterval,
                GameManager.instance.DifficultyNormalized
            );
            yield return new WaitForSeconds(interval);
        }
    }

    /// <summary>
    /// Holds _deathCooldownActive for the current difficulty-scaled interval.
    /// The spawn loop blocks on this flag so slots are never filled instantly.
    /// </summary>
    private IEnumerator DeathCooldown()
    {
        _deathCooldownActive = true;

        float cooldown = Mathf.Lerp(
            baseSpawnInterval,
            minSpawnInterval,
            GameManager.instance.DifficultyNormalized
        );
        yield return new WaitForSeconds(cooldown);

        _deathCooldownActive = false;
        _deathCooldown = null;
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

        if (spawnConfigs.Count == 0)
        {
            Debug.LogWarning("[SpawnManager] No spawn configs assigned.");
            return;
        }

        // Build valid config list into the reusable buffer — no GC allocation.
        float difficulty = GameManager.instance != null
            ? GameManager.instance.DifficultyNormalized
            : 0f;

        _validConfigsBuffer.Clear();
        foreach (EnemySpawnConfig config in spawnConfigs)
        {
            if (config.IsValidForSpawning() && difficulty >= config.difficultyThreshold)
                _validConfigsBuffer.Add(config);
        }

        if (_validConfigsBuffer.Count == 0)
        {
            Debug.LogWarning("[SpawnManager] No valid spawn configs for current difficulty.");
            return;
        }

        EnemySpawnConfig selected = SelectWeightedConfig(_validConfigsBuffer);
        if (selected == null || selected.enemyPrefab == null) return;

        GameObject enemy = Instantiate(selected.enemyPrefab, point.position, point.rotation);
        SoundManager.Instance?.PlaySFX(SoundType.EnemySpawn);

        EnemyAI ai = enemy.GetComponent<EnemyAI>();
        if (ai != null)
        {
            ai.Health = baseEnemyHealth;
            ai.MaxHealth = baseEnemyHealth;  // Keep maxHealth in sync for the health bar.
            ai.isBoss = selected.isBoss;
        }

        _activeEnemies.Add(enemy);
    }

    /// <summary>
    /// Weighted random selection from the eligible config pool.
    /// A config with weight 2 spawns twice as often as one with weight 1.
    /// </summary>
    private EnemySpawnConfig SelectWeightedConfig(List<EnemySpawnConfig> configs)
    {
        float totalWeight = 0f;
        foreach (EnemySpawnConfig c in configs) totalWeight += c.spawnWeight;
        if (totalWeight <= 0f) return null;

        float pick = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (EnemySpawnConfig c in configs)
        {
            cumulative += c.spawnWeight;
            if (pick <= cumulative) return c;
        }

        return configs[configs.Count - 1];  // Floating-point fallback — should never reach.
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the live enemy count, pruning null entries caused by Unity's
    /// deferred Destroy. Kept O(n) and called only when the loop needs a slot check.
    /// </summary>
    private int ActiveEnemyCount()
    {
        for (int i = _activeEnemies.Count - 1; i >= 0; i--)
            if (_activeEnemies[i] == null) _activeEnemies.RemoveAt(i);
        return _activeEnemies.Count;
    }

    /// <summary>
    /// Returns a spawn point at least occupiedRadius away (XZ only) from every
    /// living enemy. Y is excluded — airborne enemies should not block a slot.
    /// Returns null if all points are occupied (spawn skipped for this cycle).
    /// Starts from a random index to avoid always trying the same point first.
    /// </summary>
    private Transform GetFreeSpawnPoint()
    {
        float radiusSqr = occupiedRadius * occupiedRadius;
        int start = Random.Range(0, spawnPoints.Length);

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            Transform point = spawnPoints[(start + i) % spawnPoints.Length];
            bool occupied = false;

            for (int j = _activeEnemies.Count - 1; j >= 0; j--)
            {
                if (_activeEnemies[j] == null) { _activeEnemies.RemoveAt(j); continue; }

                // Flatten to XZ — vertical separation is irrelevant for overlap checks.
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
}