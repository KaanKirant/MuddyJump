using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("HUD")]
    [SerializeField] private HUDController hud;

    [Header("Menus")]
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private GameObject gameOverMenu;

    private bool isPaused;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void UpdateScore(int score)
    {
        hud.UpdateScore(score);
    }

    public void UpdateBestScore(int score)
    {
        hud.UpdateBestScore(score);
    }

    public void UpdateHealth(float current, float max)
    {
        hud.UpdateHealth(current, max);
    }

    public void ShowPauseMenu()
    {
        isPaused = true;

        Time.timeScale = 0f;

        pauseMenu.SetActive(true);
    }

    public void HidePauseMenu()
    {
        isPaused = false;

        Time.timeScale = 1f;

        pauseMenu.SetActive(false);
    }

    public void ShowGameOver()
    {
        Time.timeScale = 0f;

        gameOverMenu.SetActive(true);
    }

    public bool IsPaused()
    {
        return isPaused;
    }
}