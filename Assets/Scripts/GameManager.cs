using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central game loop driver. Owns platform rise speed, difficulty ramp,
/// pipe speed scaling, score tracking, and game-over flow.
///
/// Platform is moved in LateUpdate (not Update) so all physics, AI, and
/// animation have already run before the world position shifts.
/// This eliminates the 1-frame positional lag that caused flickering on
/// the player, enemies, and world-space UI when rising at speed.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Platform")]
    public Transform platformRoot;

    [Header("Platform Speed")]
    public float baseRiseSpeed = 2f;
    public float maxRiseSpeed = 12f;
    public float speedRampRate = 0.05f;

    [Header("Pipe Difficulty")]
    public PipeLogic mainPipe;
    public PipeLogic secondPipe;
    public float basePipeSpeed = 50f;
    public float maxPipeSpeed = 120f;
    public float pipeSpeedDifficultyScale = 1f;

    [Header("Second Pipe")]
    public float secondPipeUnlockDistance = 100f;
    public float secondPipeSpeedRatio = 0.75f;

    [Header("Camera")]
    public CameraController cameraController;

    public float DistanceTraveled { get; private set; }
    public int BonusScore { get; private set; }
    public int TotalScore => Mathf.FloorToInt(DistanceTraveled) + BonusScore;
    public float CurrentRiseSpeed { get; private set; }
    public bool IsGameActive { get; private set; } = true;

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

        // Difficulty and score run in Update — they don't affect transforms directly
        RampDifficulty();
        CheckSecondPipe();
        UpdateHUD();
    }

    private void LateUpdate()
    {
        if (!IsGameActive) return;

        // Platform moves in LateUpdate — after all Update() calls have run.
        // This ensures the player, enemies, and UI followers are already positioned
        // for this frame before the platform shifts, eliminating the 1-frame flicker.
        RisePlatform();
    }

    #endregion

    #region Platform

    private void RisePlatform()
    {
        float delta = CurrentRiseSpeed * Time.deltaTime;

        if (platformRoot != null)
            platformRoot.position += Vector3.up * delta;

        DistanceTraveled += delta;

        cameraController?.RiseToFloor(
            platformRoot != null ? platformRoot.position.y : DistanceTraveled
        );
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