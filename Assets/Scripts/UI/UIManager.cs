using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Central UI controller. Routes score data and game-over state from GameManager to UI.
///
/// Health display is handled directly by HUDController (subscribes to PlayerStats).
/// UIManager no longer needs to sit in the middle of that flow.
///
/// Connections:
///   GameManager → UpdateScore(), UpdateBestScore(), ShowGameOver()
///   Buttons     → wired in Awake via onClick.AddListener
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
    [Tooltip("Shown only when this run beats the saved best score.")]
    [SerializeField] private GameObject newBestLabel;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;

    [Header("Game Over Animation")]
    [Tooltip("Fade-in duration in unscaled seconds (timeScale = 0 at game over).")]
    [SerializeField] private float gameOverFadeInDuration = 0.4f;

    // ─── Pause ────────────────────────────────────────────────────────────────
    [Header("Pause")]
    [SerializeField] private GameObject pauseMenu;

    // ─── Private ──────────────────────────────────────────────────────────────
    private CanvasGroup _gameOverCanvasGroup;
    private bool _isPaused;

    // Cached before the session starts so ShowGameOver can detect a new best
    // even after GameManager.SaveBestScore() has already written to PlayerPrefs
    private int _bestScoreAtSessionStart;

    private const string BestScoreKey = "BEST_SCORE";

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        SetupGameOverPanel();

        restartButton?.onClick.AddListener(OnRestartClicked);
        mainMenuButton?.onClick.AddListener(OnMainMenuClicked);
    }

    private void Start()
    {
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

        // Auto-add CanvasGroup so fade works without manual Inspector setup
        _gameOverCanvasGroup = gameOverPanel.GetComponent<CanvasGroup>()
                            ?? gameOverPanel.AddComponent<CanvasGroup>();

        _gameOverCanvasGroup.alpha = 0f;
        _gameOverCanvasGroup.interactable = false;
        _gameOverCanvasGroup.blocksRaycasts = false;
        gameOverPanel.SetActive(false);

        if (newBestLabel != null) newBestLabel.SetActive(false);
    }

    #endregion

    #region HUD

    public void UpdateScore(int score) => hud?.UpdateScore(score);
    public void UpdateBestScore(int score) => hud?.UpdateBestScore(score);

    #endregion

    #region Game Over

    /// <summary>
    /// Called by GameManager.EndGame(). Populates panel then fades it in.
    /// Time.timeScale is already 0 — timing uses unscaled delta.
    /// </summary>
    public void ShowGameOver()
    {
        if (gameOverPanel == null) return;

        int finalScore = GameManager.instance != null ? GameManager.instance.TotalScore : 0;
        int bestScore = PlayerPrefs.GetInt(BestScoreKey, 0);

        if (gameOverScoreText != null) gameOverScoreText.text = finalScore.ToString();
        if (gameOverBestScoreText != null) gameOverBestScoreText.text = bestScore.ToString();

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

    private void OnRestartClicked()
    {
        SoundManager.Instance?.PlaySFX(SoundType.UIClick);
        GameManager.instance?.RestartGame();
    }
    private void OnMainMenuClicked()
    {
        SoundManager.Instance?.PlaySFX(SoundType.UIClick);
        GameManager.instance?.LoadMainMenu();
    } 
    #endregion
}