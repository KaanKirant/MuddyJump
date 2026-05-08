using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("UI")]
    public GameObject gameOverUI;
    public GameObject winUI;

    [Header("Floor / Elevation")]
    public Transform platformRoot;
    public float floorRiseHeight = 10f;
    public float riseSpeed = 4f;

    [Header("Pipe Difficulty")]
    public PipeLogic mainPipe;
    public PipeLogic secondPipe;
    public int secondPipeUnlockFloor = 20;
    public float basePipeSpeed = 50f;
    public float pipeSpeedIncreasePerFloor = 2f;
    public float maxPipeSpeed = 120f;

    [Header("Camera")]
    public CameraController cameraController;

    public bool isGameActive { get; private set; } = true;
    public int score { get; private set; }
    public int currentFloor { get; private set; }

    private float _currentFloorWorldY;

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (secondPipe != null)
            secondPipe.gameObject.SetActive(false);

        NextFloor();
    }

    public void NextFloor()
    {
        currentFloor++;
        Debug.Log($"[GameManager] Floor {currentFloor} started.");

        ApplyDifficulty();
        StartCoroutine(RiseToNextFloor());
    }

    private void ApplyDifficulty()
    {
        float newSpeed = Mathf.Clamp(
            basePipeSpeed + pipeSpeedIncreasePerFloor * currentFloor,
            basePipeSpeed,
            maxPipeSpeed
        );

        mainPipe.rotationSpeed = newSpeed;

        if (secondPipe != null && currentFloor >= secondPipeUnlockFloor)
        {
            secondPipe.gameObject.SetActive(true);
            secondPipe.rotationSpeed = newSpeed * 0.75f;
        }
    }

    private IEnumerator RiseToNextFloor()
    {
        if (platformRoot == null)
        {
            SpawnManager.instance.StartFloor(currentFloor);
            yield break;
        }

        Vector3 start = platformRoot.position;
        Vector3 target = start + Vector3.up * floorRiseHeight;
        float duration = floorRiseHeight / riseSpeed;
        float elapsed = 0f;

        _currentFloorWorldY += floorRiseHeight;
        cameraController?.RiseToFloor(_currentFloorWorldY);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            platformRoot.position = Vector3.Lerp(start, target, elapsed / duration);
            yield return null;
        }

        platformRoot.position = target;
        SpawnManager.instance.StartFloor(currentFloor);
    }

    public void EndGame()
    {
        if (!isGameActive) return;
        isGameActive = false;

        if (gameOverUI != null) gameOverUI.SetActive(true);
        Time.timeScale = 0f;
        Debug.Log("[GameManager] Game Over.");
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void AddScore(int amount)
    {
        if (!isGameActive) return;
        score += amount;
        Debug.Log($"[GameManager] Score: {score}");
    }
}