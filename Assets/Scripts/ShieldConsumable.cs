/// <summary>
/// Consumable that activates the player's shield.
/// The shield absorbs one hit — including the second pipe's instant kill.
/// Will not pick up if the player already has a shield active.
/// </summary>
public class ShieldConsumable : ConsumableItem
{
    /// <summary>Only usable when no shield is already active.</summary>
    protected override bool CanUse(PlayerMovement player)
    {
        return !player.IsShieldActive;
    }

    protected override void Use(PlayerMovement player)
    {
        player.ActivateShield();
    }
}