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
    public CameraController cameraController;   // Drag the Main Camera here

    public bool isGameActive = true;
    public int score = 0;
    public int currentFloor = 0;

    private float currentFloorWorldY = 0f;      // Tracks the world Y of the current platform

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
        Debug.Log($"Floor {currentFloor} started.");

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

    private System.Collections.IEnumerator RiseToNextFloor()
    {
        if (platformRoot == null)
        {
            SpawnManager.instance.StartFloor(currentFloor);
            yield break;
        }

        Vector3 start = platformRoot.position;
        Vector3 target = start + Vector3.up * floorRiseHeight;
        float elapsed = 0f;
        float duration = floorRiseHeight / riseSpeed;

        // Tell the camera where it's heading before the rise begins
        currentFloorWorldY += floorRiseHeight;
        if (cameraController != null)
            cameraController.RiseToFloor(currentFloorWorldY);

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
        gameOverUI.SetActive(true);
        Time.timeScale = 0f;
        Debug.Log("GAME OVER!");
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void AddScore(int amount)
    {
        if (!isGameActive) return;
        score += amount;
        Debug.Log("Score: " + score);
    }
}