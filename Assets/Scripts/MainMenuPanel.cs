using UnityEngine;
using UnityEngine.UI;


public class MainMenuPanel : MonoBehaviour
{
    #region Public API

    /// <summary>Shows the MainMenu panel.</summary>
    public void Show()
    {
        SoundManager.Instance?.PlaySFX(SoundType.UIClick);
        gameObject.SetActive(true);
    }

    /// <summary>Hides the MainMenu panel.</summary>
    public void Hide()
    {
        SoundManager.Instance?.PlaySFX(SoundType.UIClick);
        gameObject.SetActive(false);
    }

    #endregion
}
