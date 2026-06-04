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
    
    private static readonly string[] s_loadingTipKeys = {
        "loading_tip_0",
        "loading_tip_1",
        "loading_tip_2",
        "loading_tip_3",
        "loading_tip_4"
    };

    private void Start()
    {
        if (tipText != null)
        {
            int randomIndex = Random.Range(0, s_loadingTipKeys.Length);
            tipText.text = LocalizationManager.Instance.Get(s_loadingTipKeys[randomIndex]);
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