using UnityEngine;

/// <summary>
/// Abstract base class for all world-space consumable pickups.
///
/// Subclasses implement CanUse() (optional gate) and Use() (the effect).
/// The base class owns the trigger detection, sound, VFX, and self-destruction
/// so subclasses stay minimal and focused on their single responsibility.
///
/// Open/Closed Principle: add new consumable types by subclassing — no changes
/// to this file or any other system are required.
///
/// Inspector setup (on the prefab root):
///   - Add a trigger Collider (SphereCollider recommended, Is Trigger = true)
///   - Assign pickupEffectPrefab for a spawn-on-collect VFX (optional)
///   - Set lifetime > 0 to auto-destroy the item if not collected (0 = never)
/// </summary>
public abstract class ConsumableItem : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Pickup Settings")]
    [Tooltip("Particle/VFX prefab instantiated at the collection point. Optional.")]
    [SerializeField] private GameObject pickupEffectPrefab;

    [Tooltip("Seconds before the item self-destructs if not collected. 0 = never expires.")]
    [SerializeField] private float lifetime = 0f;

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    private void Start()
    {
        // Schedule auto-destruction only when a non-zero lifetime is configured.
        if (lifetime > 0f)
            Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only the player can collect consumables.
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;

        // Subclass gate — e.g. HealthConsumable blocks pickup at full health.
        if (!CanUse(player)) return;

        Use(player);

        SoundManager.Instance?.PlaySFX(SoundType.ConsumablePickup);

        if (pickupEffectPrefab != null)
            Instantiate(pickupEffectPrefab, transform.position, Quaternion.identity);

        // Notify the spawn manager so it can schedule the next consumable immediately.
        ConsumableSpawnManager.instance?.OnConsumableCollected();

        Destroy(gameObject);
    }

    // ─── Subclass Contract ────────────────────────────────────────────────────

    /// <summary>
    /// Override to add a pickup condition.
    /// The item is silently skipped (stays in the world) when this returns false.
    /// Default: always usable.
    /// </summary>
    protected virtual bool CanUse(PlayerMovement player) => true;

    /// <summary>
    /// Apply this consumable's effect to the player.
    /// Called only when CanUse() returns true.
    /// </summary>
    protected abstract void Use(PlayerMovement player);
}