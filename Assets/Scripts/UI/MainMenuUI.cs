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

    [Tooltip("The MainMenuPanel component in this canvas. Starts shown.")]
    [SerializeField] private MainMenuPanel mainMenuPanel;

    [Tooltip("The SettingsPanel component in this canvas. Starts hidden.")]
    [SerializeField] private SettingsPanel settingsPanel;

    [Tooltip("The InventoryPanel component in this canvas. Starts shown.")]
    [SerializeField] private MainMenuPanel inventoryPanel;

    [Tooltip("The StorePanel component in this canvas. Starts hidden.")]
    [SerializeField] private MainMenuPanel storePanel;

    private void Start()
    {
        SoundManager.Instance?.PlayMusic(MusicType.MainMenu);
        mainMenuPanel.Show();
    }

    public void PlayGame()
    {
        Time.timeScale = 1f;   // Safety reset — in case player came from a paused/game-over state
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void OpenSettings()
    {
        settingsPanel?.Show();

        if(mainMenuPanel != null)
        {
            mainMenuPanel.Hide();
        }
        if (inventoryPanel != null)
        {
            inventoryPanel.Hide();
        }
        if (storePanel != null)
        {
            storePanel.Hide();
        }
    }

    public void OpenMainMenu()
    {
        mainMenuPanel?.Show();

        if (settingsPanel != null)
        {
            settingsPanel.Hide();
        }
        if(inventoryPanel != null)
        {
            inventoryPanel.Hide();
        }
        if(storePanel != null)
        {
            storePanel.Hide();
        }
    }
    public void OpenInventory()
    {
        inventoryPanel?.Show();

        if (settingsPanel != null)
        {
            settingsPanel.Hide();
        }
        if (mainMenuPanel != null)
        {
            mainMenuPanel.Hide();
        }
        if (storePanel != null)
        {
            storePanel.Hide();
        }
    }
    public void OpenStore()
    {
        storePanel?.Show();

        if (settingsPanel != null)
        {
            settingsPanel.Hide();
        }
        if(mainMenuPanel != null)
        {
            mainMenuPanel.Hide();
        }
        if (inventoryPanel != null)
        {
            inventoryPanel.Hide();
        }
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