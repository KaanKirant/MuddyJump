/// <summary>
/// Consumable that restores exactly 1 HP to the player.
/// Will not pick up if the player is already at full health —
/// no overhealing.
/// </summary>
public class HealthConsumable : ConsumableItem
{
    /// <summary>Only usable when the player is missing at least 1 HP.</summary>
    protected override bool CanUse(PlayerMovement player)
    {
        return PlayerStats.Instance != null &&
               PlayerStats.Instance.Health < PlayerStats.Instance.MaxHealth;
    }

    protected override void Use(PlayerMovement player)
    {
        PlayerStats.Instance.Heal(1f);
    }
}