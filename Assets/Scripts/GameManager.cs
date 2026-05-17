using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central game loop driver.
///
/// Platform movement is gone — the arena is static in world space.
/// The illusion of upward movement is handled entirely by BackgroundScroller.
/// GameManager still owns DifficultyNormalized and DistanceTraveled (score).
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Difficulty / Score")]
    public float baseRiseSpeed = 2f;
    public float maxRiseSpeed = 12f;
    [Tooltip("Speed units added per second — controls how quickly difficulty ramps.")]
    public float speedRampRate = 0.05f;

    [Header("Pipe Difficulty")]
    public PipeLogic mainPipe;
    public PipeLogic secondPipe;
    public float basePipeSpeed = 50f;
    public float maxPipeSpeed = 120f;
    public float pipeSpeedDifficultyScale = 1f;

    [Header("Second Pipe")]
    [Tooltip("Distance traveled before the second pipe activates.")]
    public float secondPipeUnlockDistance = 100f;

    [Header("Camera")]
    public CameraController cameraController;

    // ─── Public State ─────────────────────────────────────────────────────────
    public float DistanceTraveled { get; private set; }
    public int BonusScore { get; private set; }
    public int TotalScore => Mathf.FloorToInt(DistanceTraveled) + BonusScore;
    public float CurrentRiseSpeed { get; private set; }
    public bool IsGameActive { get; private set; } = true;

    /// <summary>
    /// 0→1 difficulty. Single source of truth — read by SpawnManager,
    /// EnemyAI, PipeLogic scaling, and BackgroundScroller.
    /// </summary>
    public float DifficultyNormalized =>
        Mathf.InverseLerp(baseRiseSpeed, maxRiseSpeed, CurrentRiseSpeed);

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

    #region Difficulty & Distance

    private void RampDifficulty()
    {
        CurrentRiseSpeed = Mathf.Min(
            CurrentRiseSpeed + speedRampRate * Time.deltaTime,
            maxRiseSpeed
        );

        float t = DifficultyNormalized * pipeSpeedDifficultyScale;
        float pipeSpeed = Mathf.Lerp(basePipeSpeed, maxPipeSpeed, t);

        if (mainPipe != null) mainPipe.rotationSpeed = pipeSpeed;

        if (_secondPipeUnlocked && secondPipe != null)
            secondPipe.rotationSpeed = pipeSpeed;   // Second pipe matches main — it's already harder by design
    }

    /// <summary>
    /// Distance no longer comes from physical platform movement.
    /// CurrentRiseSpeed acts as a virtual "metres per second" for score purposes.
    /// </summary>
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
    /// Triggers a brief hit-stop pause for arcade impact feedback.
    /// Conservative values: timescale 0.1 for 0.04s provides snappy feel without input lag.
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