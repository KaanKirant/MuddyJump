using UnityEngine;

/// <summary>
/// Consumable that grants the player a one-hit absorbing shield.
///
/// Collecting a second shield while one is already active simply refreshes it
/// (HasShield is set to true again and the visual is re-activated), so there is
/// no wasted pickup — the item is always consumed.
///
/// The shield break logic (invincibility window, camera shake, SFX) lives in
/// PlayerMovement.BreakShield() to keep all damage-response behaviour in one place.
/// </summary>
public class ShieldConsumable : ConsumableItem
{
    // ─── ConsumableItem Contract ──────────────────────────────────────────────

    /// <summary>Always allow pickup — shields refresh rather than stack.</summary>
    protected override bool CanUse(PlayerMovement player) => true;

    /// <summary>Activates the player's shield via PlayerMovement.GrantShield().</summary>
    protected override void Use(PlayerMovement player)
    {
        player.GrantShield();
    }
}