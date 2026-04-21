using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    // The Singleton instance
    public static GameManager instance;
    public GameObject gameOverUI;

    public bool isGameActive = true;
    public int score = 0;

    private void Awake()
    {
        // Setup Singleton
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    public void EndGame()
    {
        if (!isGameActive) return;

        isGameActive = false;
        gameOverUI.SetActive(true);
        Debug.Log("GAME OVER!");

        // Freeze time (optional, creates a dramatic pause)
        Time.timeScale = 0f;

        // Logic to show UI would go here
    }

    public void RestartGame()
    {
        // Unfreeze time before restarting
        Time.timeScale = 1f;
        gameOverUI.SetActive(false);
        isGameActive = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // The score is how many kicks and jumps. How many times the players kick made the enemy lose health. How to define successful jump?
    public void AddScore(int amount)
    {
        if (!isGameActive) return;
        score += amount;
        Debug.Log("Score: " + score);
    }
}
