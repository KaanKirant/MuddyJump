using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central game loop driver. Owns difficulty ramp, score, pipe base speed,
/// second pipe unlock, and game-over flow.
///
/// Pipe speed model: GameManager writes to PipeLogic.BaseSpeed each frame
/// as the difficulty floor. PipeLogic maintains its own _runtimeSpeed that
/// kicks and hits modify independently — those changes are never overwritten here.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    // ─── Difficulty ───────────────────────────────────────────────────────────
    [Header("Difficulty")]
    [Tooltip("Virtual speed at game start. Used only for DifficultyNormalized calculation.")]
    public float baseRiseSpeed = 2f;

    [Tooltip("Virtual speed at max difficulty.")]
    public float maxRiseSpeed = 12f;

    [Tooltip("How fast virtual speed ramps up per second. " +
             "0.15 reaches max difficulty in ~67 seconds. 0.05 takes ~200 seconds.")]
    public float speedRampRate = 0.15f;

    // ─── Pipe Difficulty ──────────────────────────────────────────────────────
    [Header("Pipe Base Speed")]
    public PipeLogic mainPipe;
    public PipeLogic secondPipe;

    [Tooltip("Pipe BaseSpeed at difficulty 0. Should feel slow but not trivial.")]
    public float basePipeSpeed = 60f;

    [Tooltip("Pipe BaseSpeed at difficulty 1 (max). Should feel genuinely threatening.")]
    public float maxPipeSpeed = 200f;

    [Tooltip("1 = pipe speed fully tracks difficulty. Lower = pipe stays easier than difficulty suggests.")]
    public float pipeSpeedDifficultyScale = 1f;

    // ─── Second Pipe ──────────────────────────────────────────────────────────
    [Header("Second Pipe")]
    [Tooltip("Distance accumulated before the second pipe activates.")]
    public float secondPipeUnlockDistance = 150f;

    // ─── Public State ─────────────────────────────────────────────────────────
    public float DistanceTraveled { get; private set; }
    public int BonusScore { get; private set; }
    public int TotalScore => Mathf.FloorToInt(DistanceTraveled) + BonusScore;
    public float CurrentRiseSpeed { get; private set; }
    public bool IsGameActive { get; private set; } = true;

    /// <summary>
    /// 0→1 difficulty. Single source of truth for all systems.
    /// Reaches 1 when CurrentRiseSpeed hits maxRiseSpeed.
    /// </summary>
    public float DifficultyNormalized =>
        Mathf.InverseLerp(baseRiseSpeed, maxRiseSpeed, CurrentRiseSpeed);

    // ─── Private ──────────────────────────────────────────────────────────────
    private const string BestScoreKey = "BEST_SCORE";
    private bool _secondPipeUnlocked;

    #region Unity Lifecycle

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        CurrentRiseSpeed = baseRiseSpeed;

        if (secondPipe != null)
            secondPipe.gameObject.SetActive(false);

        SpawnManager.instance.StartSpawning();
        UpdateHUD();
    }

    private void Update()
    {
        if (!IsGameActive) return;

        RampDifficulty();
        AccumulateDistance();
        CheckSecondPipe();
        UpdateHUD();
    }

    #endregion

    #region Difficulty

    private void RampDifficulty()
    {
        CurrentRiseSpeed = Mathf.Min(
            CurrentRiseSpeed + speedRampRate * Time.deltaTime,
            maxRiseSpeed
        );

        float t = DifficultyNormalized * pipeSpeedDifficultyScale;
        float pipeSpeed = Mathf.Lerp(basePipeSpeed, maxPipeSpeed, t);

        // Write to BaseSpeed — this is the difficulty floor.
        // PipeLogic._runtimeSpeed handles the live speed and is never touched here,
        // so kicks and hits keep their effect until they decay naturally.
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
        Debug.Log("[GameManager] Second pipe unlocked.");
    }

    #endregion

    #region Score

    public void AddBonusScore(int amount)
    {
        if (!IsGameActive) return;
        BonusScore += amount;
        UpdateHUD();
    }

    private void UpdateHUD()
    {
        if (UIManager.Instance == null) return;
        UIManager.Instance.UpdateScore(TotalScore);
        UIManager.Instance.UpdateBestScore(PlayerPrefs.GetInt(BestScoreKey, 0));
    }

    #endregion

    #region Game Over

    public void EndGame()
    {
        if (!IsGameActive) return;
        IsGameActive = false;

        SpawnManager.instance.StopSpawning();

        if (mainPipe != null) mainPipe.enabled = false;
        if (secondPipe != null) secondPipe.enabled = false;

        SaveBestScore();
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

    #endregion

    #region Game Feel

    /// <summary>
    /// Brief time-scale freeze for arcade impact feedback.
    /// Called by PipeLogic on hits and PlayerMovement on kicks.
    /// </summary>
    public void TriggerHitStop(float timescale = 0.1f, float duration = 0.04f)
    {
        StartCoroutine(HitStopRoutine(timescale, duration));
    }

    private System.Collections.IEnumerator HitStopRoutine(float timescale, float duration)
    {
        Time.timeScale = timescale;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }

    #endregion

    #region Scene Flow

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenuScene");
    }

    #endregion
}