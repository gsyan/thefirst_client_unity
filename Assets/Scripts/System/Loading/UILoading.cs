//------------------------------------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UILoading : UIManager
{
    [Header("Loading UI Elements")]
    public TMP_Text loadingText;
    public Image progressBar;
    public TMP_Text tipText;
    
    private string[] loadingTips = {
        "Tip: Use WASD keys to move around in space!",
        "Tip: Right-click to interact with objects.",
        "Tip: Check your inventory regularly.",
        "Tip: Don't forget to save your progress!",
        "Tip: Explore different areas to find new items."
    };
    
    private void Start()
    {
        // Display random tip
        if (tipText != null && loadingTips.Length > 0)
        {
            int randomIndex = Random.Range(0, loadingTips.Length);
            tipText.text = loadingTips[randomIndex];
        }
        
        // Pass UI reference to LoadingManager
        LoadingManager loadingManager = FindFirstObjectByType<LoadingManager>();
        if (loadingManager != null)
        {
            loadingManager.loadingText = loadingText;
            loadingManager.progressBar = progressBar;
        }

        NetworkManager.Instance.OnChangeScene();
    }
}