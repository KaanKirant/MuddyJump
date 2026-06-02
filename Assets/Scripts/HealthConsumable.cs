using UnityEngine;

/// <summary>
/// A consumable that restores 1 HP to the player when collected.
/// </summary>
public class HealthConsumable : ConsumableItem
{
    [Header("Health Consumable Settings")]
    [SerializeField] private float healthRestore = 1f;

    /// <summary>
    /// Only allow pickup if player's health is below max.
    /// </summary>
    protected override bool CanUse(PlayerMovement player)
    {
        PlayerStats stats = PlayerStats.Instance;
        if (stats == null) return false;

        return stats.Health < stats.MaxHealth;
    }

    /// <summary>
    /// Apply the health restore effect.
    /// </summary>
    protected override void Use(PlayerMovement player)
    {
        PlayerStats stats = PlayerStats.Instance;
        if (stats != null)
            stats.Heal(healthRestore);
    }
}
