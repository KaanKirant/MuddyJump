using UnityEngine;

/// <summary>
/// Consumable that restores a fixed amount of HP when collected.
///
/// The item is always consumed on contact (Subway Surfers pattern) — if the
/// player is already at full health the heal is skipped but the item still
/// disappears. This prevents awkward "phasing through" an item at full HP.
///
/// Inspector setup:
///   healthRestore — amount to heal (default 1, supports fractional values)
/// </summary>
public class HealthConsumable : ConsumableItem
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Health Consumable")]
    [Tooltip("HP restored on collection. Clamped to the player's max health by PlayerStats.")]
    [SerializeField] private float healthRestore = 1f;

    // ─── ConsumableItem Contract ──────────────────────────────────────────────

    /// <summary>
    /// No gate — health consumables are always collected and always destroyed.
    /// The conditional heal is handled inside Use() instead.
    /// </summary>
    protected override bool CanUse(PlayerMovement player) => true;

    /// <summary>
    /// Heals the player if below max health.
    /// PlayerStats.Heal() clamps the result so overhealing is impossible.
    /// </summary>
    protected override void Use(PlayerMovement player)
    {
        if (PlayerStats.Instance == null) return;

        if (PlayerStats.Instance.Health < PlayerStats.Instance.MaxHealth)
            PlayerStats.Instance.Heal(healthRestore);
    }
}