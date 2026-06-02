using UnityEngine;

/// <summary>
/// A consumable that grants a protective shield to the player when collected.
/// The shield blocks one hit and then breaks.
/// </summary>
public class ShieldConsumable : ConsumableItem
{
    /// <summary>
    /// Always allow shield pickup (shield replaces any existing shield).
    /// </summary>
    protected override bool CanUse(PlayerMovement player) => true;

    /// <summary>
    /// Grant the player a shield.
    /// </summary>
    protected override void Use(PlayerMovement player)
    {
        player.GrantShield();
    }
}
