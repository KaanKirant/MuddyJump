using UnityEngine;

/// <summary>
/// Abstract base class for all consumable pickups.
///
/// Attach a concrete subclass (HealthConsumable, ShieldConsumable) to a
/// GameObject with a trigger Collider. When the player walks into it,
/// Use() is called and the pickup destroys itself.
///
/// Inspector setup:
///   - Add a trigger Collider (e.g. SphereCollider, Is Trigger = true)
///   - Tag the GameObject "Consumable" or use a dedicated layer
///   - Assign an optional pickup effect prefab for spawn-on-collect VFX
/// </summary>
public abstract class ConsumableItem : MonoBehaviour
{
    [Header("Pickup Settings")]
    [Tooltip("Visual effect spawned at pickup position when collected. Optional.")]
    [SerializeField] private GameObject pickupEffectPrefab;

    [Tooltip("Seconds before the item destroys itself if not collected. 0 = never expires.")]
    [SerializeField] private float lifetime = 0f;

    private void Start()
    {
        if (lifetime > 0f)
            Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only the player can pick up consumables
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;

        // Let the subclass decide whether it can be used right now
        if (!CanUse(player)) return;

        Use(player);

        SoundManager.Instance?.PlaySFX(SoundType.ConsumablePickup);

        if (pickupEffectPrefab != null)
            Instantiate(pickupEffectPrefab, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }

    /// <summary>
    /// Returns true if this consumable can currently be used by the player.
    /// Override to add conditions (e.g. health must be below max for a heal).
    /// </summary>
    protected virtual bool CanUse(PlayerMovement player) => true;

    /// <summary>Apply this consumable's effect to the player.</summary>
    protected abstract void Use(PlayerMovement player);
}