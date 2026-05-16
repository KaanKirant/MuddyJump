using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central game loop driver. Owns platform rise speed, difficulty ramp,
/// pipe speed scaling, score tracking, and game-over flow.
///
/// Single source of truth: DifficultyNormalized (0→1) — all other systems
/// read this instead of maintaining their own difficulty counters.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    // ─── Platform ─────────────────────────────────────────────────────────────
    [Header("Platform")]
    [Tooltip("Root transform of the arena. Moved upward every frame.")]
    public Transform platformRoot;

    [Header("Platform Speed")]
    public float baseRiseSpeed = 2f;    // Starting units/sec
    public float maxRiseSpeed = 12f;   // Hard cap
    [Tooltip("Speed units added per second. Controls how quickly difficulty ramps.")]
    public float speedRampRate = 0.05f;

    // ─── Pipe Difficulty ──────────────────────────────────────────────────────
    [Header("Pipe Difficulty")]
    public PipeLogic mainPipe;
    public PipeLogic secondPipe;

    public float basePipeSpeed = 50f;
    public float maxPipeSpeed = 120f;
    [Tooltip("1 = pipe fully tracks platform difficulty. Lower values make pipe easier than the platform.")]
    public float pipeSpeedDifficultyScale = 1f;

    [Header("Second Pipe")]
    [Tooltip("Distance in world units before the second pipe activates.")]
    public float secondPipeUnlockDistance = 100f;
    public float secondPipeSpeedRatio = 0.75f;   // Second pipe is always a bit slower

    // ─── Camera ───────────────────────────────────────────────────────────────
    [Header("Camera")]
    public CameraController cameraController;

    // ─── Public Read-Only State ───────────────────────────────────────────────
    public float DistanceTraveled { get; private set; }
    public int BonusScore { get; private set; }

    /// <summary>Total displayed score: distance (floored) + bonus from kills/kicks.</summary>
    public int TotalScore => Mathf.FloorToInt(DistanceTraveled) + BonusScore;

    public float CurrentRiseSpeed { get; private set; }
    public bool IsGameActive { get; private set; } = true;

    /// <summary>
    /// Normalised difficulty 0→1. Read by SpawnManager, EnemyAI, PipeLogic scaling.
    /// 0 = game start speed, 1 = max speed reached.
    /// </summary>
    public float DifficultyNormalized =>
        Mathf.InverseLerp(baseRiseSpeed, maxRiseSpeed, CurrentRiseSpeed);

    // ─── Private ──────────────────────────────────────────────────────────────
    private const string BestScoreKey = "BEST_SCORE";
    private bool _secondPipeUnlocked;

    // ─────────────────────────────────────────────────────────────────────────

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

        // Initial HUD draw
        UpdateHUD();
    }

    private void Update()
    {
        if (!IsGameActive) return;

        // Only logic and UI in Update
        CheckSecondPipe();
        UpdateHUD();
    }

    private void FixedUpdate()
    {
        if (!IsGameActive) return;

        // Physics-affecting movements MUST be in FixedUpdate
        RisePlatform();
        RampDifficulty();
    }

    #endregion

    #region Platform

    private void RisePlatform()
    {
        float delta = CurrentRiseSpeed * Time.deltaTime;

        if (platformRoot != null)
            platformRoot.position += Vector3.up * delta;

        DistanceTraveled += delta;

        // Feed live platform Y to camera every frame — no lerp needed here,
        // CameraController's SmoothDamp handles the smoothing
        cameraController?.RiseToFloor(
            platformRoot != null ? platformRoot.position.y : DistanceTraveled
        );
    }

    #endregion

    #region Difficulty

    private void RampDifficulty()
    {
        // Linear ramp — simple and predictable for a hypercasual game
        CurrentRiseSpeed = Mathf.Min(
            CurrentRiseSpeed + speedRampRate * Time.deltaTime,
            maxRiseSpeed
        );

        // Pipe speed is driven by the same 0→1 normalised value
        // so it always stays proportional to platform speed
        float t = DifficultyNormalized * pipeSpeedDifficultyScale;
        float pipeSpeed = Mathf.Lerp(basePipeSpeed, maxPipeSpeed, t);

        if (mainPipe != null) mainPipe.rotationSpeed = pipeSpeed;

        if (_secondPipeUnlocked && secondPipe != null)
            secondPipe.rotationSpeed = pipeSpeed * secondPipeSpeedRatio;
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

    /// <summary>
    /// Called for discrete score events: successful kicks (+1), enemy kills (+5).
    /// Distance is added automatically every frame in RisePlatform.
    /// </summary>
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

        // Freeze pipes in place
        if (mainPipe != null) mainPipe.enabled = false;
        if (secondPipe != null) secondPipe.enabled = false;

        SaveBestScore();

        UIManager.Instance?.ShowGameOver();

        // Freeze time after UI is shown so the UI itself still animates in
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