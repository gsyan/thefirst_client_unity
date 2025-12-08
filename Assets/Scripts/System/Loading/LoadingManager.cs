//------------------------------------------------------------------------------
using System.Collections;
using TMPro;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text loadingText;
    public Image progressBar;
    
    private string targetSceneName;
    private float loadingProgress = 0f;
    
    void Start()
    {
        // Get target scene name from PlayerPrefs (default: "SpaceScene")
        targetSceneName = PlayerPrefs.GetString("TargetScene", "SpaceScene");
        
        if (progressBar != null)
            progressBar.fillAmount = loadingProgress;

        StartCoroutine(LoadSceneAsync());
    }
    
    private IEnumerator LoadSceneAsync()
    {
        // Set minimum loading time (for development/testing)
        float minimumLoadingTime = 2.0f; // Increased to 2 seconds
        float startTime = Time.time;
        
        yield return new WaitForSeconds(0.5f); // Initial wait
        
        // Start asynchronous scene loading
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetSceneName);
        // Unity automatically switches scenes when LoadSceneAsync is completed by default
        // Set allowSceneActivation = false for manual control
        // Switch by changing allowSceneActivation = true at desired timing
        asyncLoad.allowSceneActivation = false;
        
        bool sceneLoadComplete = false;
        
        while (!asyncLoad.isDone)
        {
            // Actual loading progress
            float realProgress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            
            // Minimum time-based progress
            float timeProgress = Mathf.Clamp01((Time.time - startTime) / minimumLoadingTime);
            
            // Use smaller value between the two progress rates (considering both actual loading and minimum time)
            loadingProgress = Mathf.Min(realProgress, timeProgress);
            
            UpdateLoadingUI();
            
            // When actual loading is 90% complete and minimum time has also elapsed
            if (asyncLoad.progress >= 0.9f && Time.time - startTime >= minimumLoadingTime)
            {
                if (!sceneLoadComplete)
                {
                    sceneLoadComplete = true;
                    // Loading completion animation
                    yield return StartCoroutine(CompleteLoading());
                    
                    // Activate scene
                    asyncLoad.allowSceneActivation = true;
                }
            }
            
            yield return null;
        }
        
        // Clean up PlayerPrefs
        PlayerPrefs.DeleteKey("TargetScene");
    }
    
    private IEnumerator CompleteLoading()
    {
        // Smoothly complete to 100%
        while (loadingProgress < 1f)
        {
            loadingProgress += Time.deltaTime * 2f; // Speed adjustment
            loadingProgress = Mathf.Clamp01(loadingProgress);
            UpdateLoadingUI();
            yield return null;
        }
        
        if (loadingText != null)
            loadingText.text = "Loading Complete!";
            
        yield return new WaitForSeconds(0.5f);
    }
    
    private void UpdateLoadingUI()
    {
        if (loadingText != null)
        {
            // Loading animation effect
            string dots = new string('.', (int)(Time.time * 2f) % 4);
            loadingText.text = $"Loading {targetSceneName}{dots}";
        }

        if (progressBar != null)
            progressBar.fillAmount = loadingProgress;
            
        // Debug log
        //Debug.Log($"Loading Progress: {Mathf.RoundToInt(loadingProgress * 100f)}% - Target: {targetSceneName}");
    }
    
    // Static method that can be called from external sources
    public static void LoadSceneWithLoading(string sceneName)
    {
        PlayerPrefs.SetString("TargetScene", sceneName);
        SceneManager.LoadScene("LoadingScene");
    }
}