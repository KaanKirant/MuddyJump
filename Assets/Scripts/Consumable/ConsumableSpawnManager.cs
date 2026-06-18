using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages consumable pickup spawning via a continuous interval loop.
///
/// Only one consumable exists at a time. The loop waits for the active item
/// to be collected or expire before scheduling the next spawn. This mirrors
/// the Subway Surfers pattern: items appear above the player, who must jump
/// to collect them.
///
/// Spawn timing:
///   The interval timer starts AFTER the previous item is removed (collected
///   or expired), not from the moment it spawns. This means a fast pickup
///   does not shorten the gap — the player always gets a full interval of
///   breathing room between items.
///
/// Inspector setup:
///   spawnConfigs    — list of ConsumableSpawnConfig assets (weighted)
///   spawnPoints     — world transforms the item spawns above (picked randomly)
///   spawnInterval   — seconds between items
///   spawnHeightOffset — Y units above the spawn point (must be jumpable)
///   consumableLifetime — seconds before an uncollected item self-destructs
/// </summary>
public class ConsumableSpawnManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static ConsumableSpawnManager instance;

    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Consumable Pool")]
    [Tooltip("Weighted spawn configs. Create via Right-click > Gameplay > Consumable Spawn Config.")]
    public List<ConsumableSpawnConfig> spawnConfigs = new List<ConsumableSpawnConfig>();

    [Tooltip("World transforms the item can spawn above. One is chosen at random each spawn.")]
    public Transform[] spawnPoints;

    [Header("Spawn Settings")]
    [Tooltip("Seconds between the removal of one item and the appearance of the next.")]
    public float spawnInterval = 8f;

    [Tooltip("Height above the chosen spawn point. Must be within jump range of the player.")]
    public float spawnHeightOffset = 4f;

    [Tooltip("Seconds before an uncollected item self-destructs. Passed to the ConsumableItem.")]
    public float consumableLifetime = 15f;

    // ─── Private ──────────────────────────────────────────────────────────────
    /// <summary>Reference to the currently live consumable, or null when none exists.</summary>
    private GameObject _activeConsumable;
    private Coroutine _spawnLoop;
    private bool _running;

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
    /// Stops spawning and destroys any live consumable.
    /// Called by GameManager.EndGame().
    /// </summary>
    public void StopSpawning()
    {
        _running = false;
        if (_spawnLoop != null) { StopCoroutine(_spawnLoop); _spawnLoop = null; }

        if (_activeConsumable != null)
        {
            Destroy(_activeConsumable);
            _activeConsumable = null;
        }
    }

    /// <summary>
    /// Called by ConsumableItem.OnTriggerEnter() when the player collects the item.
    /// Clears the reference so the spawn loop knows to schedule the next item.
    /// </summary>
    public void OnConsumableCollected()
    {
        // The GameObject destroys itself — just clear the reference here.
        _activeConsumable = null;
    }

    // ─── Spawn Loop ───────────────────────────────────────────────────────────

    /// <summary>
    /// Core loop:
    ///   1. Wait until the current item is gone (collected or expired).
    ///   2. Wait the configured interval.
    ///   3. Spawn a new item and repeat.
    ///
    /// The interval comes AFTER the item is gone, not after it spawns — this
    /// ensures the full gap is always felt regardless of how quickly the player
    /// collects items.
    /// </summary>
    private IEnumerator SpawnLoop()
    {
        while (_running)
        {
            // Wait for the active item to be removed (Unity null-check catches Destroy'd objects).
            yield return new WaitUntil(() => _activeConsumable == null || !_running);
            if (!_running) yield break;

            // Full interval gap between removal and next spawn.
            yield return new WaitForSeconds(spawnInterval);
            if (!_running) yield break;

            SpawnConsumable();
        }
    }

    // ─── Spawning ─────────────────────────────────────────────────────────────

    private void SpawnConsumable()
    {
        if (spawnPoints.Length == 0)
        {
            Debug.LogWarning("[ConsumableSpawnManager] No spawn points assigned.");
            return;
        }

        if (spawnConfigs.Count == 0)
        {
            Debug.LogWarning("[ConsumableSpawnManager] No spawn configs assigned.");
            return;
        }

        ConsumableSpawnConfig selected = SelectWeightedConfig();
        if (selected == null) return;

        // Choose a random spawn point and offset upward so the item floats above it.
        Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
        Vector3 spawnPos = point.position + Vector3.up * spawnHeightOffset;

        _activeConsumable = Instantiate(selected.consumablePrefab, spawnPos, Quaternion.identity);

        // Note: item lifetime is controlled by the ConsumableItem.lifetime SerializedField on the prefab.
        // To centralise lifetime tuning here, expose a SetLifetime(float) method on ConsumableItem.

        SoundManager.Instance?.PlaySFX(SoundType.ConsumableSpawn);
    }

    /// <summary>
    /// Weighted random selection across all valid configs.
    /// Invalid configs (null prefab or zero weight) are silently skipped.
    /// Returns null only when no valid config exists — caller handles this.
    /// </summary>
    private ConsumableSpawnConfig SelectWeightedConfig()
    {
        float totalWeight = 0f;
        foreach (ConsumableSpawnConfig c in spawnConfigs)
            if (c.IsValidForSpawning()) totalWeight += c.spawnWeight;

        if (totalWeight <= 0f)
        {
            Debug.LogWarning("[ConsumableSpawnManager] No valid spawn configs.");
            return null;
        }

        float pick = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (ConsumableSpawnConfig c in spawnConfigs)
        {
            if (!c.IsValidForSpawning()) continue;
            cumulative += c.spawnWeight;
            if (pick <= cumulative) return c;
        }

        return null; // Should never reach here if totalWeight > 0.
    }
}