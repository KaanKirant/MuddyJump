using UnityEngine;

/// <summary>
/// Owns the player's health values and fires a change notification whenever
/// health or max-health changes. All reads and writes go through this class
/// so every system (UI, consumables, regen) shares a single source of truth.
///
/// Singleton accessed via PlayerStats.Instance — one instance per gameplay scene.
/// The instance is set in Awake, so it is safe to read from any Start() or later.
///
/// Inspector setup:
///   health          — starting HP (e.g. 3)
///   maxHealth       — current heart-container count (≤ maxTotalHealth)
///   maxTotalHealth  — absolute cap; also drives HealthBarController array size
/// </summary>
public class PlayerStats : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static PlayerStats Instance { get; private set; }

    // ─── Change Notification ──────────────────────────────────────────────────
    /// <summary>
    /// Fired after any health or max-health change.
    /// Subscribe in OnEnable, unsubscribe in OnDisable or OnDestroy.
    /// </summary>
    public event System.Action OnHealthChanged;

    // ─── Serialized Fields ────────────────────────────────────────────────────
    [Header("Health")]
    [Tooltip("Starting health. Must be <= maxHealth.")]
    [SerializeField] private float health = 3f;

    [Tooltip("Current heart-container count. Caps health. Must be <= maxTotalHealth.")]
    [SerializeField] private float maxHealth = 3f;

    [Tooltip("Absolute upper bound for maxHealth. Also sizes the heart-container array.")]
    [SerializeField] private float maxTotalHealth = 5f;

    // ─── Public Read API ──────────────────────────────────────────────────────
    public float Health => health;
    public float MaxHealth => maxHealth;
    public float MaxTotalHealth => maxTotalHealth;

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        // Standard singleton — one instance per scene, no DontDestroyOnLoad
        // (each gameplay scene creates its own fresh instance).
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        // Clear the static reference so a reloaded scene gets a clean start.
        if (Instance == this) Instance = null;
    }

    // ─── Public Write API ─────────────────────────────────────────────────────

    /// <summary>Restores <paramref name="amount"/> HP, clamped to maxHealth.</summary>
    public void Heal(float amount)
    {
        health += amount;
        ClampAndNotify();
    }

    /// <summary>Reduces health by <paramref name="amount"/>, clamped to 0.</summary>
    public void TakeDamage(float amount)
    {
        health -= amount;
        ClampAndNotify();
    }

    // ─── Private Helpers ──────────────────────────────────────────────────────

    /// <summary>Clamps health to [0, maxHealth] then fires the change event.</summary>
    private void ClampAndNotify()
    {
        health = Mathf.Clamp(health, 0f, maxHealth);
        OnHealthChanged?.Invoke();
    }
}