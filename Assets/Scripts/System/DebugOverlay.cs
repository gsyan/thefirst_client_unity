using UnityEngine;
using TMPro;

public class DebugOverlay : MonoSingleton<DebugOverlay>
{
    private TextMeshProUGUI m_debugText;
    private Canvas m_canvas;

    protected override bool ShouldDontDestroyOnLoad => true;

    protected override void OnInitialize()
    {
        base.OnInitialize();
        CreateDebugUI();
    }

    private void CreateDebugUI()
    {
        GameObject canvasGO = new GameObject("DebugCanvas");
        canvasGO.transform.SetParent(transform);

        m_canvas = canvasGO.AddComponent<Canvas>();
        m_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        m_canvas.sortingOrder = 9999;

        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        GameObject textGO = new GameObject("DebugText");
        textGO.transform.SetParent(canvasGO.transform);

        m_debugText = textGO.AddComponent<TextMeshProUGUI>();
        m_debugText.fontSize = 48;
        m_debugText.color = Color.white;
        m_debugText.alignment = TextAlignmentOptions.TopLeft;

        RectTransform rt = m_debugText.rectTransform;
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(50, -200);
        rt.sizeDelta = new Vector2(800, 300);
    }

    public void SetText(string text)
    {
        if (m_debugText != null)
            m_debugText.text = text;
    }
}
