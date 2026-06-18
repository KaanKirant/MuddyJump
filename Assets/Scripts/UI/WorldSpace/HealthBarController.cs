using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the player's heart-based health bar in the HUD.
///
/// Subscribes to PlayerStats.OnHealthChanged so the display updates
/// immediately on any health change without any per-frame polling.
///
/// Heart containers are instantiated once on Start based on maxTotalHealth.
/// Each frame only the fill amounts and active states are updated — no
/// allocations occur during gameplay.
///
/// Inspector setup:
///   heartsParent         — RectTransform that holds the instantiated hearts
///   heartContainerPrefab — Prefab with a child Image named "HeartFill"
///                          (Image type = Filled, FillMethod = Horizontal or Radial360)
/// </summary>
public class HealthBarController : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Heart Display")]
    [Tooltip("Parent RectTransform that holds the instantiated heart container GameObjects.")]
    [SerializeField] private Transform heartsParent;

    [Tooltip("Prefab with a child Image named 'HeartFill' (Filled image type).")]
    [SerializeField] private GameObject heartContainerPrefab;

    // ─── Private ──────────────────────────────────────────────────────────────
    private GameObject[] _heartContainers;
    private Image[] _heartFills;

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    private void Start()
    {
        InstantiateHeartContainers();
        UpdateHeartsHUD();

        // Subscribe after building the UI so the first forced refresh above
        // never races with an event fired during Start().
        PlayerStats.Instance.OnHealthChanged += UpdateHeartsHUD;
    }

    private void OnDestroy()
    {
        // Guard against the case where PlayerStats is destroyed first.
        if (PlayerStats.Instance != null)
            PlayerStats.Instance.OnHealthChanged -= UpdateHeartsHUD;
    }

    // ─── HUD Update ───────────────────────────────────────────────────────────

    /// <summary>
    /// Refreshes heart container visibility and fill amounts to match current health.
    /// Called once on Start and then automatically via the OnHealthChanged event.
    /// </summary>
    public void UpdateHeartsHUD()
    {
        SetHeartContainers();
        SetFilledHearts();
    }

    /// <summary>
    /// Shows heart containers up to maxHealth, hides the rest.
    /// Allows runtime heart-slot changes if maxHealth ever grows.
    /// </summary>
    private void SetHeartContainers()
    {
        for (int i = 0; i < _heartContainers.Length; i++)
            _heartContainers[i].SetActive(i < PlayerStats.Instance.MaxHealth);
    }

    /// <summary>
    /// Sets each fill amount to 1 (full), 0 (empty), or a partial value
    /// for the fractional heart produced by regen or partial damage.
    /// </summary>
    private void SetFilledHearts()
    {
        float health = PlayerStats.Instance.Health;

        for (int i = 0; i < _heartFills.Length; i++)
            _heartFills[i].fillAmount = i < health ? 1f : 0f;

        // Handle partial (fractional) heart — only one slot can be partial at a time.
        if (health % 1f != 0f)
        {
            int partialSlot = Mathf.FloorToInt(health);
            if (partialSlot < _heartFills.Length)
                _heartFills[partialSlot].fillAmount = health % 1f;
        }
    }

    // ─── Initialisation ───────────────────────────────────────────────────────

    /// <summary>
    /// Instantiates one heart container per maxTotalHealth slot.
    /// Runs once in Start; never called again during play.
    /// </summary>
    private void InstantiateHeartContainers()
    {
        int total = Mathf.RoundToInt(PlayerStats.Instance.MaxTotalHealth);
        _heartContainers = new GameObject[total];
        _heartFills = new Image[total];

        for (int i = 0; i < total; i++)
        {
            GameObject container = Instantiate(heartContainerPrefab, heartsParent, false);
            _heartContainers[i] = container;

            Transform fill = container.transform.Find("HeartFill");
            if (fill != null)
                _heartFills[i] = fill.GetComponent<Image>();
            else
                Debug.LogWarning($"[HealthBarController] Heart prefab slot {i} missing 'HeartFill' child.");
        }
    }
}