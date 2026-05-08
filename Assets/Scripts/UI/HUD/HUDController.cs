using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDController : MonoBehaviour
{
    [Header("Score")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text bestScoreText;

    [Header("Health")]
    [SerializeField] private Slider healthSlider;

    public void UpdateScore(int score)
    {
        scoreText.text = score.ToString();
    }

    public void UpdateBestScore(int score)
    {
        bestScoreText.text = $"BEST: {score}";
    }

    public void UpdateHealth(float current, float max)
    {
        healthSlider.maxValue = max;
        healthSlider.value = current;
    }
}