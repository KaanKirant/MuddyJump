using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    // ─── Platform ─────────────────────────────────────────────────────────────
    [Header("Platform")]
    public Transform platformRoot;

    [Header("Platform Speed")]
    public float baseRiseSpeed = 2f;
    public float maxRiseSpeed = 12f;
    public float speedRampRate = 0.05f;

    // ─── Pipe Difficulty ──────────────────────────────────────────────────────
    [Header("Pipe Difficulty")]
    public PipeLogic mainPipe;
    public PipeLogic secondPipe;

    public float basePipeSpeed = 50f;
    public float maxPipeSpeed = 120f;

    [Tooltip("How strongly pipe speed tracks difficulty. 1 = full scale.")]
    public float pipeSpeedDifficultyScale = 1f;

    [Header("Second Pipe")]
    [Tooltip("World units traveled before second pipe activates.")]
    public float secondPipeUnlockDistance = 100f;

    public float secondPipeSpeedRatio = 0.75f;

    // ─── Camera ───────────────────────────────────────────────────────────────
    [Header("Camera")]
    public CameraController cameraController;

    // ─── Public State ─────────────────────────────────────────────────────────
    public float DistanceTraveled { get; private set; }

    public int BonusScore { get; private set; }

    public int TotalScore =>
        Mathf.FloorToInt(DistanceTraveled) + BonusScore;

    public float CurrentRiseSpeed { get; private set; }

    public bool IsGameActive { get; private set; } = true;

    // Normalised 0→1 difficulty — single source of truth
    public float DifficultyNormalized =>
        Mathf.InverseLerp(baseRiseSpeed, maxRiseSpeed, CurrentRiseSpeed);

    // ─── Save Keys ────────────────────────────────────────────────────────────
    private const string BestScoreKey = "BEST_SCORE";

    private bool _secondPipeUnlocked;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
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
        if (!IsGameActive)
            return;

        RisePlatform();
        RampDifficulty();
        CheckSecondPipe();

        UpdateHUD();
    }

    // ─── Platform ─────────────────────────────────────────────────────────────

    private void RisePlatform()
    {
        float delta = CurrentRiseSpeed * Time.deltaTime;

        if (platformRoot != null)
            platformRoot.position += Vector3.up * delta;

        DistanceTraveled += delta;

        if (cameraController != null)
        {
            cameraController.RiseToFloor(
                platformRoot != null
                    ? platformRoot.position.y
                    : DistanceTraveled
            );
        }
    }

    // ─── Difficulty ───────────────────────────────────────────────────────────

    private void RampDifficulty()
    {
        CurrentRiseSpeed = Mathf.Min(
            CurrentRiseSpeed + speedRampRate * Time.deltaTime,
            maxRiseSpeed
        );

        float t = DifficultyNormalized * pipeSpeedDifficultyScale;

        float pipeSpeed =
            Mathf.Lerp(basePipeSpeed, maxPipeSpeed, t);

        if (mainPipe != null)
            mainPipe.rotationSpeed = pipeSpeed;

        if (_secondPipeUnlocked && secondPipe != null)
            secondPipe.rotationSpeed =
                pipeSpeed * secondPipeSpeedRatio;
    }

    private void CheckSecondPipe()
    {
        if (_secondPipeUnlocked || secondPipe == null)
            return;

        if (DistanceTraveled < secondPipeUnlockDistance)
            return;

        _secondPipeUnlocked = true;

        secondPipe.gameObject.SetActive(true);

        Debug.Log("[GameManager] Second pipe unlocked.");
    }

    // ─── Score ────────────────────────────────────────────────────────────────

    public void AddBonusScore(int amount)
    {
        if (!IsGameActive)
            return;

        BonusScore += amount;

        UpdateHUD();
    }

    private void UpdateHUD()
    {
        if (UIManager.Instance == null)
            return;

        UIManager.Instance.UpdateScore(TotalScore);

        int bestScore = PlayerPrefs.GetInt(BestScoreKey, 0);

        UIManager.Instance.UpdateBestScore(bestScore);
    }

    // ─── Game Over ────────────────────────────────────────────────────────────

    public void EndGame()
    {
        if (!IsGameActive)
            return;

        IsGameActive = false;

        SpawnManager.instance.StopSpawning();

        // Stop pipes
        if (mainPipe != null)
            mainPipe.enabled = false;

        if (secondPipe != null)
            secondPipe.enabled = false;

        SaveBestScore();

        if (UIManager.Instance != null)
            UIManager.Instance.ShowGameOver();

        Time.timeScale = 0f;

        Debug.Log($"[GameManager] Game Over. Score: {TotalScore}");
    }

    private void SaveBestScore()
    {
        int currentBest =
            PlayerPrefs.GetInt(BestScoreKey, 0);

        if (TotalScore <= currentBest)
            return;

        PlayerPrefs.SetInt(BestScoreKey, TotalScore);
        PlayerPrefs.Save();
    }

    // ─── Scene Flow ───────────────────────────────────────────────────────────

    public void RestartGame()
    {
        Time.timeScale = 1f;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;

        SceneManager.LoadScene("MainMenuScene");
    }
}