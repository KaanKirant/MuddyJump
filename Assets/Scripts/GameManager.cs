using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    // ─── UI ───────────────────────────────────────────────────────────────────
    [Header("UI")]
    public GameObject gameOverUI;

    // ─── Platform ─────────────────────────────────────────────────────────────
    [Header("Platform")]
    public Transform platformRoot;

    [Header("Platform Speed")]
    public float baseRiseSpeed = 2f;     // Units/sec at game start
    public float maxRiseSpeed = 12f;    // Hard cap
    public float speedRampRate = 0.05f;  // Speed added per second (linear ramp)

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
    public int TotalScore => Mathf.FloorToInt(DistanceTraveled) + BonusScore;
    public float CurrentRiseSpeed { get; private set; }
    public bool IsGameActive { get; private set; } = true;

    // Normalised 0→1 difficulty — single source of truth for all systems
    public float DifficultyNormalized =>
        Mathf.InverseLerp(baseRiseSpeed, maxRiseSpeed, CurrentRiseSpeed);

    private bool _secondPipeUnlocked;

    // ─────────────────────────────────────────────────────────────────────────

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
    }

    private void Update()
    {
        if (!IsGameActive) return;

        RisePlatform();
        RampDifficulty();
        CheckSecondPipe();
    }

    // ─── Platform ─────────────────────────────────────────────────────────────

    private void RisePlatform()
    {
        float delta = CurrentRiseSpeed * Time.deltaTime;

        if (platformRoot != null)
            platformRoot.position += Vector3.up * delta;

        DistanceTraveled += delta;

        // Camera tracks platform Y every frame
        if (cameraController != null)
            cameraController.RiseToFloor(platformRoot != null
                ? platformRoot.position.y
                : DistanceTraveled);
    }

    // ─── Difficulty ───────────────────────────────────────────────────────────

    private void RampDifficulty()
    {
        CurrentRiseSpeed = Mathf.Min(
            CurrentRiseSpeed + speedRampRate * Time.deltaTime,
            maxRiseSpeed
        );

        // Pipe speed lerps between base and max using the same normalised t
        float t = DifficultyNormalized * pipeSpeedDifficultyScale;
        float pipeSpeed = Mathf.Lerp(basePipeSpeed, maxPipeSpeed, t);

        if (mainPipe != null)
            mainPipe.rotationSpeed = pipeSpeed;

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

    // ─── Score ────────────────────────────────────────────────────────────────

    // For kicks, kills, and any other bonus events
    public void AddBonusScore(int amount)
    {
        if (!IsGameActive) return;
        BonusScore += amount;
    }

    // ─── Game Over ────────────────────────────────────────────────────────────

    public void EndGame()
    {
        if (!IsGameActive) return;
        IsGameActive = false;

        SpawnManager.instance.StopSpawning();

        // Stop pipes
        if (mainPipe != null) mainPipe.enabled = false;
        if (secondPipe != null) secondPipe.enabled = false;

        if (gameOverUI != null) gameOverUI.SetActive(true);
        Time.timeScale = 0f;

        Debug.Log($"[GameManager] Game Over. Score: {TotalScore}");
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}