using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central game loop driver. Owns difficulty ramp, score, pipe base speed,
/// second pipe unlock, and game-over flow.
///
/// Difficulty model:
///   DifficultyNormalized (0→1) is driven purely by time elapsed.
///   It reaches 1 at timeToMaxDifficulty seconds. All systems read this
///   single value — no virtual platform speed, no distance accumulation
///   used for difficulty purposes.
///
/// Score:
///   Score is time-based (seconds survived) plus BonusScore from kicks/kills.
///
/// Pipe speed model:
///   GameManager writes to PipeLogic.BaseSpeed each frame as the difficulty
///   floor. PipeLogic keeps its own _runtimeSpeed that kicks and hits modify
///   independently — those values are never overwritten here.
///
/// HUD updates:
///   Score text updates only when the integer score changes — not every frame.
/// </summary>
public class GameManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static GameManager instance;

    // ─── Difficulty ───────────────────────────────────────────────────────────
    [Header("Difficulty")]
    [Tooltip("Seconds to reach maximum difficulty (DifficultyNormalized = 1).")]
    public float timeToMaxDifficulty = 120f;

    // ─── Pipe Speed ───────────────────────────────────────────────────────────
    [Header("Pipe Speed")]
    [Tooltip("Main (non-lethal) pipe reference.")]
    public PipeLogic mainPipe;

    [Tooltip("Second (lethal) pipe reference. Inactive until secondPipeUnlockTime.")]
    public PipeLogic secondPipe;

    [Tooltip("Pipe BaseSpeed at difficulty 0. Should feel slow but not trivial.")]
    public float basePipeSpeed = 70f;

    [Tooltip("Pipe BaseSpeed at difficulty 1 (max). Should feel genuinely threatening.")]
    public float maxPipeSpeed = 220f;

    // ─── Second Pipe ──────────────────────────────────────────────────────────
    [Header("Second Pipe")]
    [Tooltip("Seconds elapsed before the second pipe activates.")]
    public float secondPipeUnlockTime = 60f;

    // ─── Consumables ──────────────────────────────────────────────────────────
    [Header("Consumables")]
    [Tooltip("Consumable spawn manager. Assign the ConsumableSpawnManager in the scene.")]
    public ConsumableSpawnManager consumableSpawnManager;

    // ─── Public State ─────────────────────────────────────────────────────────
    public int BonusScore { get; private set; }
    public int TotalScore => Mathf.FloorToInt(_timeElapsed) + BonusScore;
    public bool IsGameActive { get; private set; } = true;

    /// <summary>
    /// 0→1 difficulty curve. Single source of truth for all systems.
    /// Driven purely by time — reaches 1 at timeToMaxDifficulty seconds.
    /// </summary>
    public float DifficultyNormalized =>
        Mathf.Clamp01(_timeElapsed / timeToMaxDifficulty);

    // ─── Private ──────────────────────────────────────────────────────────────
    private const string BestScoreKey = "BEST_SCORE";

    private float _timeElapsed;
    private bool _secondPipeUnlocked;
    private int _lastReportedScore;  // Guards against redundant HUD refreshes

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }

        // Mobile performance defaults — SettingsPanel may override targetFrameRate.
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
    }

    private void Start()
    {
        if (secondPipe != null)
            secondPipe.gameObject.SetActive(false);

        SpawnManager.instance.StartSpawning();
        consumableSpawnManager?.StartSpawning();

        UpdateHUD();

        SoundManager.Instance?.PlayMusic(MusicType.Gameplay);
    }

    private void Update()
    {
        if (!IsGameActive) return;

        _timeElapsed += Time.deltaTime;

        UpdatePipeSpeed();
        CheckSecondPipe();

        if (TotalScore != _lastReportedScore)
            UpdateHUD();
    }

    // ─── Difficulty ───────────────────────────────────────────────────────────

    /// <summary>
    /// Writes the current difficulty-scaled speed to PipeLogic.BaseSpeed every frame.
    /// PipeLogic._runtimeSpeed handles live modifications from kicks and hits
    /// and is never touched here.
    /// </summary>
    private void UpdatePipeSpeed()
    {
        float pipeSpeed = Mathf.Lerp(basePipeSpeed, maxPipeSpeed, DifficultyNormalized);

        if (mainPipe != null) mainPipe.BaseSpeed = pipeSpeed;
        if (_secondPipeUnlocked && secondPipe != null)
            secondPipe.BaseSpeed = pipeSpeed;
    }

    private void CheckSecondPipe()
    {
        if (_secondPipeUnlocked || secondPipe == null) return;
        if (_timeElapsed < secondPipeUnlockTime) return;

        _secondPipeUnlocked = true;
        secondPipe.gameObject.SetActive(true);
        SoundManager.Instance?.PlaySFX(SoundType.SecondPipeWarning);
    }

    // ─── Score ────────────────────────────────────────────────────────────────

    /// <summary>Awards bonus score points and immediately refreshes the HUD.</summary>
    public void AddBonusScore(int amount)
    {
        if (!IsGameActive) return;
        BonusScore += amount;
        UpdateHUD();
    }

    private void UpdateHUD()
    {
        if (UIManager.Instance == null) return;
        _lastReportedScore = TotalScore;
        UIManager.Instance.UpdateScore(TotalScore);
        UIManager.Instance.UpdateBestScore(PlayerPrefs.GetInt(BestScoreKey, 0));
    }

    // ─── Game Over ────────────────────────────────────────────────────────────

    /// <summary>
    /// Ends the current run. Disables spawning and pipes, saves best score,
    /// triggers game-over UI, and freezes time.
    /// </summary>
    public void EndGame()
    {
        if (!IsGameActive) return;
        IsGameActive = false;

        SpawnManager.instance.StopSpawning();
        consumableSpawnManager?.StopSpawning();

        if (mainPipe != null) mainPipe.enabled = false;
        if (secondPipe != null) secondPipe.enabled = false;

        SaveBestScore();

        SoundManager.Instance?.PlaySFX(SoundType.GameOver);
        SoundManager.Instance?.StopMusic();

        UIManager.Instance?.ShowGameOver();
        Time.timeScale = 0f;

        Debug.Log($"[GameManager] Game Over. Score: {TotalScore}");
    }

    private void SaveBestScore()
    {
        int best = PlayerPrefs.GetInt(BestScoreKey, 0);
        if (TotalScore <= best) return;
        PlayerPrefs.SetInt(BestScoreKey, TotalScore);
        PlayerPrefs.Save();
    }

    // ─── Game Feel ────────────────────────────────────────────────────────────

    /// <summary>
    /// Brief time-scale freeze for arcade impact feedback.
    /// Uses unscaled real time so the freeze itself is never affected by timeScale.
    /// Called by PipeLogic on hits and PlayerMovement on kicks.
    /// </summary>
    /// <param name="timescale">Time scale during the freeze (0.1 = nearly stopped).</param>
    /// <param name="duration">Real-time seconds to hold the freeze.</param>
    public void TriggerHitStop(float timescale = 0.1f, float duration = 0.04f)
    {
        StartCoroutine(HitStopRoutine(timescale, duration));
    }

    private IEnumerator HitStopRoutine(float timescale, float duration)
    {
        Time.timeScale = timescale;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }

    // ─── Scene Flow ───────────────────────────────────────────────────────────

    /// <summary>Reloads the current scene, resetting all gameplay state.</summary>
    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>Loads the main menu scene.</summary>
    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenuScene");
    }
}