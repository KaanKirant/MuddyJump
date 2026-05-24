using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Minimal main menu controller.
/// Wire PlayGame() and QuitGame() to Button.onClick in the Inspector.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Tooltip("Exact name of your gameplay scene as it appears in Build Settings.")]
    [SerializeField] private string gameplaySceneName = "GameplayScene";

    [Tooltip("The SettingsPanel component in this canvas. Starts hidden.")]
    [SerializeField] private SettingsPanel settingsPanel;

    private void Start()
    {
        SoundManager.Instance?.PlayMusic(MusicType.MainMenu); 
    }

    public void PlayGame()
    {
        Time.timeScale = 1f;   // Safety reset — in case player came from a paused/game-over state
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void OpenSettings()
    {
        settingsPanel?.Show();
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}