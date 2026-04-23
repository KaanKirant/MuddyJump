using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    public GameObject gameOverUI;
    public GameObject winUI;
    public bool isGameActive = true;
    public int score = 0;
    public int currentFloor = 0;

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Kick off the first floor when the game starts
        NextFloor();
    }

    public void NextFloor()
    {
        currentFloor++;
        Debug.Log("Starting floor: " + currentFloor);
        SpawnManager.instance.SpawnFloor(currentFloor);
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
        gameOverUI.SetActive(false);
        isGameActive = true;
        currentFloor = 0;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void AddScore(int amount)
    {
        if (!isGameActive) return;
        score += amount;
        Debug.Log("Score: " + score);
    }
}