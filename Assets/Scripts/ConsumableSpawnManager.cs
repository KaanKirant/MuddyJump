using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages consumable spawning via a continuous interval loop.
///
/// Consumables spawn high in the air (above player jump reach initially) so they
/// require the player to jump to collect them. This makes them more rewarding.
///
/// Spawning is purely time-driven with configurable intervals.
/// Only one consumable can exist at a time — when collected or expired, the next
/// one spawns after the configured delay.
/// </summary>
public class ConsumableSpawnManager : MonoBehaviour
{
    public static ConsumableSpawnManager instance;

    // ─── Consumable Setup ─────────────────────────────────────────────────────
    [Header("Consumable Setup")]
    [Tooltip("Spawn configurations for each consumable type. Create via Right-click > Gameplay > Consumable Spawn Config.")]
    public List<ConsumableSpawnConfig> spawnConfigs = new List<ConsumableSpawnConfig>();

    [Tooltip("Transforms representing valid spawn positions in the scene.")]
    public Transform[] spawnPoints;

    // ─── Spawn Settings ───────────────────────────────────────────────────────
    [Header("Spawn Settings")]
    [Tooltip("Seconds between consumable spawns.")]
    public float spawnInterval = 8f;

    [Tooltip("Height above spawn point where consumable appears (must be reachable by jumping).")]
    public float spawnHeightOffset = 4f;

    [Tooltip("Lifetime of a spawned consumable before it disappears if not collected.")]
    public float consumableLifetime = 15f;

    // ─── Private ──────────────────────────────────────────────────────────────
    private GameObject _activeConsumable;
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

    /// <summary>Starts the consumable spawn loop. Called by GameManager.Start().</summary>
    public void StartSpawning()
    {
        _running = true;
        if (_spawnLoop != null) StopCoroutine(_spawnLoop);
        _spawnLoop = StartCoroutine(SpawnLoop());
    }

    /// <summary>Stops spawning and destroys the active consumable. Called by GameManager.EndGame().</summary>
    public void StopSpawning()
    {
        _running = false;
        if (_spawnLoop != null) { StopCoroutine(_spawnLoop); _spawnLoop = null; }
        if (_activeConsumable != null) Destroy(_activeConsumable);
        _activeConsumable = null;
    }

    /// <summary>Called when a consumable is collected by the player.</summary>
    public void OnConsumableCollected()
    {
        _activeConsumable = null;
    }

    #endregion

    #region Spawn Loop

    private IEnumerator SpawnLoop()
    {
        while (_running)
        {
            // Wait until there's no active consumable
            yield return new WaitUntil(() => _activeConsumable == null || !_running);
            if (!_running) yield break;

            SpawnConsumable();

            // Wait before spawning the next one
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    #endregion

    #region Spawning

    private void SpawnConsumable()
    {
        if (spawnPoints.Length == 0)
        {
            Debug.LogWarning("[ConsumableSpawnManager] No spawn points assigned.");
            return;
        }

        Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
        if (point == null) return;

        if (spawnConfigs.Count == 0)
        {
            Debug.LogWarning("[ConsumableSpawnManager] No spawn configs assigned.");
            return;
        }

        List<ConsumableSpawnConfig> validConfigs = new List<ConsumableSpawnConfig>();
        foreach (var config in spawnConfigs)
        {
            if (config.IsValidForSpawning())
                validConfigs.Add(config);
        }

        if (validConfigs.Count == 0)
        {
            Debug.LogWarning("[ConsumableSpawnManager] No valid spawn configs.");
            return;
        }

        ConsumableSpawnConfig selected = SelectWeightedConfig(validConfigs);
        if (selected == null || selected.consumablePrefab == null) return;

        // Spawn high in the air so it requires jumping to reach
        Vector3 spawnPosition = point.position + Vector3.up * spawnHeightOffset;
        _activeConsumable = Instantiate(selected.consumablePrefab, spawnPosition, Quaternion.identity);

        // Update the consumable's lifetime if it has a ConsumableItem component
        ConsumableItem consumable = _activeConsumable.GetComponent<ConsumableItem>();
        if (consumable != null)
        {
            // We'll rely on ConsumableItem's lifetime setting, but can override if needed
        }

        SoundManager.Instance?.PlaySFX(SoundType.ConsumableSpawn);
    }

    /// <summary>
    /// Weighted random selection from the eligible config pool.
    /// A config with weight 2 spawns twice as often as one with weight 1.
    /// </summary>
    private ConsumableSpawnConfig SelectWeightedConfig(List<ConsumableSpawnConfig> configs)
    {
        float totalWeight = 0f;
        foreach (var c in configs) totalWeight += c.spawnWeight;
        if (totalWeight <= 0f) return null;

        float pick = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (var c in configs)
        {
            cumulative += c.spawnWeight;
            if (pick <= cumulative) return c;
        }

        return configs[configs.Count - 1];   // Fallback — should never reach
    }

    #endregion
}
