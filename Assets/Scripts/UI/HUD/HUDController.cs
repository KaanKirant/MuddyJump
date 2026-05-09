using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the in-game HUD: score text, best score text, and heart display.
///
/// Heart display uses two parallel arrays:
///   heartImages  — the heart sprite (full/empty)
///   regenRings   — radial fill Image overlaid on each heart (FillMethod = Radial360)
///
/// UpdateHearts() is called by UIManager every frame during regen and on
/// instant life changes (damage, full recovery).
///
/// Inspector setup:
///   heartImages  — assign the same number of Image components as maxLives (default 3)
///   regenRings   — assign one radial-fill Image per heart slot (can be null to skip rings)
///   fullHeart / emptyHeart — sprites swapped based on current life count
/// </summary>
public class HUDController : MonoBehaviour
{
    [Header("Score")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text bestScoreText;

    [Header("Hearts")]
    [Tooltip("One Image per heart slot. Count must match maxLives in PlayerMovement.")]
    [SerializeField] private Image[] heartImages;
    [SerializeField] private Sprite fullHeart;
    [SerializeField] private Sprite emptyHeart;

    [Header("Regen Rings")]
    [Tooltip("One radial-fill Image per heart slot. Must be same length as heartImages. " +
             "Set ImageType = Filled, FillMethod = Radial360, FillOrigin = Top in Inspector.")]
    [SerializeField] private Image[] regenRings;

    [Tooltip("Color of the regen ring fill.")]
    [SerializeField] private Color regenRingColor = new Color(1f, 0.6f, 0.6f, 0.9f);

    #region Score

    public void UpdateScore(int score)
    {
        if (scoreText != null)
            scoreText.text = score.ToString();
    }

    public void UpdateBestScore(int score)
    {
        if (bestScoreText != null)
            bestScoreText.text = $"BEST  {score}";
    }

    #endregion

    #region Hearts

    /// <summary>
    /// Updates heart sprites and regen ring fill.
    /// Called by UIManager.OnLivesChanged(int lives, float regenProgress).
    ///
    /// lives         = current life count
    /// regenProgress = 0→1 fill on the NEXT heart to recover (0 = no active regen)
    ///
    /// Heart i is:
    ///   full     if i < lives
    ///   regening if i == lives and regenProgress > 0  (shows fill ring)
    ///   empty    if i > lives
    /// </summary>
    public void UpdateHearts(int lives, float regenProgress)
    {
        for (int i = 0; i < heartImages.Length; i++)
        {
            if (heartImages[i] == null) continue;

            bool isFull = i < lives;
            bool isRegening = i == lives && regenProgress > 0f;

            // Sprite swap
            heartImages[i].sprite = isFull ? fullHeart : emptyHeart;
            heartImages[i].enabled = true;

            // Regen ring — only active on the next-to-fill slot
            if (regenRings != null && i < regenRings.Length && regenRings[i] != null)
            {
                regenRings[i].color = regenRingColor;
                regenRings[i].fillAmount = isRegening ? regenProgress : 0f;
            }
        }
    }

    /// <summary>
    /// Legacy method kept for any direct callers passing float health values.
    /// Routes through the new UpdateHearts signature with no regen progress.
    /// </summary>
    public void UpdateHealth(float current, float max)
    {
        UpdateHearts(Mathf.RoundToInt(current), 0f);
    }

    #endregion
}