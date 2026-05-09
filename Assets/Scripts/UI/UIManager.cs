using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Central UI controller. Owns all panels and routes data from game systems to UI components.
///
/// Connections:
///   GameManager  → UpdateScore(), UpdateBestScore(), ShowGameOver()
///   PlayerMovement.OnLivesChanged → OnLivesChanged() → HUDController.UpdateHearts()
///
/// Game over panel uses a CanvasGroup for fade-in with unscaled time
/// (Time.timeScale = 0 at game over so WaitForSecondsRealtime is required).
/// Buttons are wired in code — no UnityEvent setup needed in Inspector.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    // ─── HUD ──────────────────────────────────────────────────────────────────
    [Header("HUD")]
    [SerializeField] private HUDController hud;

    // ─── Game Over ────────────────────────────────────────────────────────────
    [Header("Game Over Panel")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI gameOverScoreText;
    [SerializeField] private TextMeshProUGUI gameOverBestScoreText;
    [Tooltip("'NEW BEST!' label — activated only when this run beats the saved best.")]
    [SerializeField] private GameObject newBestLabel;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;

    [Header("Game Over Animation")]
    [Tooltip("Seconds for the game over panel to fade in (unscaled time).")]
    [SerializeField] private float gameOverFadeInDuration = 0.4f;

    // ─── Pause ────────────────────────────────────────────────────────────────
    [Header("Pause")]
    [SerializeField] private GameObject pauseMenu;

    // ─── Private ──────────────────────────────────────────────────────────────
    private CanvasGroup _gameOverCanvasGroup;
    private bool _isPaused;

    // Cache best score at start so ShowGameOver can compare correctly
    // even after GameManager has already saved the new best
    private int _bestScoreAtSessionStart;

    private const string BestScoreKey = "BEST_SCORE";

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        SetupGameOverPanel();

        // Wire buttons in code — avoids Inspector UnityEvent fragility on scene reload
        restartButton?.onClick.AddListener(OnRestartClicked);
        mainMenuButton?.onClick.AddListener(OnMainMenuClicked);
    }

    private void Start()
    {
        // Cache best score before this session can overwrite it
        _bestScoreAtSessionStart = PlayerPrefs.GetInt(BestScoreKey, 0);
    }

    private void OnDestroy()
    {
        restartButton?.onClick.RemoveListener(OnRestartClicked);
        mainMenuButton?.onClick.RemoveListener(OnMainMenuClicked);
    }

    #endregion

    #region Setup

    private void SetupGameOverPanel()
    {
        if (gameOverPanel == null) return;

        // Auto-add CanvasGroup if missing so we don't require manual Inspector setup
        _gameOverCanvasGroup = gameOverPanel.GetComponent<CanvasGroup>()
                            ?? gameOverPanel.AddComponent<CanvasGroup>();

        _gameOverCanvasGroup.alpha = 0f;
        _gameOverCanvasGroup.interactable = false;
        _gameOverCanvasGroup.blocksRaycasts = false;
        gameOverPanel.SetActive(false);

        if (newBestLabel != null)
            newBestLabel.SetActive(false);
    }

    #endregion

    #region HUD

    /// <summary>Called by GameManager every frame and on bonus score events.</summary>
    public void UpdateScore(int score) => hud?.UpdateScore(score);

    /// <summary>Called by GameManager every frame with the current PlayerPrefs best.</summary>
    public void UpdateBestScore(int score) => hud?.UpdateBestScore(score);

    /// <summary>
    /// Routed from PlayerMovement.OnLivesChanged.
    /// lives = current life count, regenProgress = 0→1 fill (0 on instant changes).
    /// </summary>
    private void OnLivesChanged(int lives, float regenProgress)
    {
        hud?.UpdateHearts(lives, regenProgress);
    }

    #endregion

    #region Game Over

    /// <summary>
    /// Called by GameManager.EndGame(). Populates and fades in the game over panel.
    /// Time.timeScale is already 0 — all timing uses unscaled time.
    /// </summary>
    public void ShowGameOver()
    {
        if (gameOverPanel == null) return;

        int finalScore = GameManager.instance != null ? GameManager.instance.TotalScore : 0;
        int bestScore = PlayerPrefs.GetInt(BestScoreKey, 0);

        if (gameOverScoreText != null) gameOverScoreText.text = finalScore.ToString();
        if (gameOverBestScoreText != null) gameOverBestScoreText.text = bestScore.ToString();

        // Compare against pre-session best — GameManager.SaveBestScore() already ran
        if (newBestLabel != null)
            newBestLabel.SetActive(finalScore > _bestScoreAtSessionStart);

        gameOverPanel.SetActive(true);
        StartCoroutine(FadeInGameOver());
    }

    private IEnumerator FadeInGameOver()
    {
        if (_gameOverCanvasGroup == null) yield break;

        float elapsed = 0f;
        _gameOverCanvasGroup.alpha = 0f;
        _gameOverCanvasGroup.interactable = false;
        _gameOverCanvasGroup.blocksRaycasts = false;

        while (elapsed < gameOverFadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _gameOverCanvasGroup.alpha = Mathf.Clamp01(elapsed / gameOverFadeInDuration);
            yield return null;
        }

        // Fully visible and interactive
        _gameOverCanvasGroup.alpha = 1f;
        _gameOverCanvasGroup.interactable = true;
        _gameOverCanvasGroup.blocksRaycasts = true;
    }

    #endregion

    #region Pause

    public void ShowPauseMenu()
    {
        _isPaused = true;
        Time.timeScale = 0f;
        if (pauseMenu != null) pauseMenu.SetActive(true);
    }

    public void HidePauseMenu()
    {
        _isPaused = false;
        Time.timeScale = 1f;
        if (pauseMenu != null) pauseMenu.SetActive(false);
    }

    public bool IsPaused() => _isPaused;

    #endregion

    #region Button Handlers

    private void OnRestartClicked() => GameManager.instance?.RestartGame();
    private void OnMainMenuClicked() => GameManager.instance?.LoadMainMenu();

    #endregion
}