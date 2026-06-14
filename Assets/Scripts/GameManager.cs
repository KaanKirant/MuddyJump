using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central game loop driver. Owns difficulty ramp, score, pipe base speed,
/// second pipe unlock, and game-over flow.
///
/// Pipe speed model:
///   GameManager writes to PipeLogic.BaseSpeed each frame as the difficulty
///   floor. PipeLogic keeps its own _runtimeSpeed that kicks and hits modify
///   independently — those values are never overwritten here.
///
/// HUD updates:
///   Score text is updated only when the score actually changes (AddBonusScore,
///   distance accumulation) rather than every Update tick, eliminating a
///   per-frame UIManager call when nothing has changed.
/// </summary>
public class GameManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static GameManager instance;

    // ─── Difficulty ───────────────────────────────────────────────────────────
    [Header("Difficulty")]
    [Tooltip("Virtual speed at game start. Used only for DifficultyNormalized calculation.")]
    public float baseRiseSpeed = 2f;

    [Tooltip("Virtual speed at max difficulty.")]
    public float maxRiseSpeed = 12f;

    [Tooltip("How fast virtual speed ramps up per second. " +
             "0.15 reaches max difficulty in ~67 s. 0.05 takes ~200 s.")]
    public float speedRampRate = 0.15f;

    // ─── Pipe Difficulty ──────────────────────────────────────────────────────
    [Header("Pipe Base Speed")]
    [Tooltip("Main (non-lethal) pipe reference.")]
    public PipeLogic mainPipe;

    [Tooltip("Second (lethal) pipe reference. Inactive until secondPipeUnlockDistance.")]
    public PipeLogic secondPipe;

    [Tooltip("Pipe BaseSpeed at difficulty 0. Should feel slow but not trivial.")]
    public float basePipeSpeed = 60f;

    [Tooltip("Pipe BaseSpeed at difficulty 1 (max). Should feel genuinely threatening.")]
    public float maxPipeSpeed = 200f;

    [Tooltip("1 = pipe speed fully tracks difficulty. Lower = pipe stays easier than difficulty.")]
    public float pipeSpeedDifficultyScale = 1f;

    // ─── Second Pipe ──────────────────────────────────────────────────────────
    [Header("Second Pipe")]
    [Tooltip("Distance accumulated before the second pipe activates.")]
    public float secondPipeUnlockDistance = 150f;

    // ─── Consumables ──────────────────────────────────────────────────────────
    [Header("Consumables")]
    [Tooltip("Consumable spawn manager. Assign the ConsumableSpawnManager in the scene.")]
    public ConsumableSpawnManager consumableSpawnManager;

    // ─── Public State ─────────────────────────────────────────────────────────
    public float DistanceTraveled { get; private set; }
    public int BonusScore { get; private set; }
    public int TotalScore => Mathf.FloorToInt(DistanceTraveled) + BonusScore;
    public float CurrentRiseSpeed { get; private set; }
    public bool IsGameActive { get; private set; } = true;

    /// <summary>
    /// 0→1 difficulty curve. Single source of truth for all systems.
    /// Reaches 1 when CurrentRiseSpeed hits maxRiseSpeed.
    /// </summary>
    public float DifficultyNormalized =>
        Mathf.InverseLerp(baseRiseSpeed, maxRiseSpeed, CurrentRiseSpeed);

    // ─── Private ──────────────────────────────────────────────────────────────
    private const string BestScoreKey = "BEST_SCORE";

    private bool _secondPipeUnlocked;
    private int _lastReportedScore;   // Guards against redundant HUD refreshes

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }

        // Mobile performance defaults — SettingsPanel may override targetFrameRate later.
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
    }

    private void Start()
    {
        CurrentRiseSpeed = baseRiseSpeed;

        if (secondPipe != null)
            secondPipe.gameObject.SetActive(false);

        SpawnManager.instance.StartSpawning();
        consumableSpawnManager?.StartSpawning();

        // Initial HUD paint.
        UpdateHUD();

        SoundManager.Instance?.PlayMusic(MusicType.Gameplay);
    }

    private void Update()
    {
        if (!IsGameActive) return;

        RampDifficulty();
        AccumulateDistance();
        CheckSecondPipe();

        // Only refresh the HUD when the integer score value has actually changed —
        // avoids a SetText call every frame when distance hasn't moved a full unit.
        if (TotalScore != _lastReportedScore)
            UpdateHUD();
    }

    // ─── Difficulty ───────────────────────────────────────────────────────────

    private void RampDifficulty()
    {
        CurrentRiseSpeed = Mathf.Min(
            CurrentRiseSpeed + speedRampRate * Time.deltaTime,
            maxRiseSpeed
        );

        float t = DifficultyNormalized * pipeSpeedDifficultyScale;
        float pipeSpeed = Mathf.Lerp(basePipeSpeed, maxPipeSpeed, t);

        // Write difficulty floor to BaseSpeed.
        // PipeLogic._runtimeSpeed is never touched here — kicks and hits keep their effect.
        if (mainPipe != null) mainPipe.BaseSpeed = pipeSpeed;

        if (_secondPipeUnlocked && secondPipe != null)
            secondPipe.BaseSpeed = pipeSpeed;
    }

    private void AccumulateDistance()
    {
        DistanceTraveled += CurrentRiseSpeed * Time.deltaTime;
    }

    private void CheckSecondPipe()
    {
        if (_secondPipeUnlocked || secondPipe == null) return;
        if (DistanceTraveled < secondPipeUnlockDistance) return;

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