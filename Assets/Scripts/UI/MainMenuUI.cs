using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    public void PlayGame()
    {
        Time.timeScale = 1f;

        SceneManager.LoadScene("GameplayScene");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}