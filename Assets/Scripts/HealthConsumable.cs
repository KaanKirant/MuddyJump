using UnityEngine;

/// <summary>
/// A consumable that restores 1 HP to the player when collected.
/// </summary>
public class HealthConsumable : ConsumableItem
{
    [Header("Health Consumable Settings")]
    [SerializeField] private float healthRestore = 1f;

    /// <summary>
    /// Apply the health restore effect.
    /// </summary>
    protected override void Use(PlayerMovement player)
    {
        PlayerStats stats = PlayerStats.Instance;
        if (stats == null) return;

        // Only heal if below max — but always destroy the item (Subway Surfers pattern)
        if (stats.Health < stats.MaxHealth)
            stats.Heal(healthRestore);
    }
}
