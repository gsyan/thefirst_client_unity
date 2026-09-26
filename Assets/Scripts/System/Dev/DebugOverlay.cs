using System.Collections;
using UnityEngine;
using TMPro;

public class DebugOverlay : MonoSingleton<DebugOverlay>
{
    private TMP_Text m_debugText;
    private TMP_Text m_fpsText;
    private Canvas m_canvas;

    // FPS 표시 — 평균 갱신 주기, 글자 크기/여백은 화면 짧은 변 대비 비율
    private const float k_fpsSampleIntervalSec = 0.5f;
    private const float k_fpsFontSizeRatio = 0.035f;
    private const float k_fpsMarginRatio = 0.02f;
    private static readonly WaitForSecondsRealtime s_fpsSampleWait = new WaitForSecondsRealtime(k_fpsSampleIntervalSec);

    protected override bool ShouldDontDestroyOnLoad => true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // dev 빌드는 앱 시작 시 오버레이(FPS 표시)를 자동 생성
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateInDevBuild()
    {
        _ = Instance;
    }
#endif

    protected override void OnInitialize()
    {
        base.OnInitialize();
        CreateDebugUI();
        CreateFpsUI();
        StartCoroutine(FpsRoutine());
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
        m_debugText.raycastTarget = false; // 화면 위 빈 텍스트 영역이 UI 터치를 막지 않도록

        RectTransform rt = m_debugText.rectTransform;
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(50, -200);
        rt.sizeDelta = new Vector2(800, 300);
    }

    private void CreateFpsUI()
    {
        GameObject textGO = new GameObject("FpsText");
        textGO.transform.SetParent(m_canvas.transform, false);

        m_fpsText = textGO.AddComponent<TextMeshProUGUI>();
        m_fpsText.color = Color.green;
        m_fpsText.alignment = TextAlignmentOptions.BottomLeft;
        m_fpsText.raycastTarget = false;

        RectTransform rt = m_fpsText.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        ApplyFpsLayout();
    }

    // 화면 회전/세이프에어리어 변경에 맞춰 좌측 하단 위치와 글자 크기를 갱신
    private void ApplyFpsLayout()
    {
        float shortSide = Mathf.Min(Screen.width, Screen.height);
        float fontSize = shortSide * k_fpsFontSizeRatio;
        float margin = shortSide * k_fpsMarginRatio;
        Rect safeArea = Screen.safeArea;

        m_fpsText.fontSize = fontSize;
        RectTransform rt = m_fpsText.rectTransform;
        rt.anchoredPosition = new Vector2(safeArea.xMin + margin, safeArea.yMin + margin);
        rt.sizeDelta = new Vector2(fontSize * 20f, fontSize * 1.5f);
    }

    // 주기마다 프레임 수 증가량 / 실시간 경과로 평균 FPS와 프레임 시간(ms) 계산 — timeScale 영향 없음
    private IEnumerator FpsRoutine()
    {
        int lastFrameCount = Time.frameCount;
        float lastTime = Time.realtimeSinceStartup;
        while (true)
        {
            yield return s_fpsSampleWait;

            int frameCount = Time.frameCount - lastFrameCount;
            float elapsedSec = Time.realtimeSinceStartup - lastTime;
            lastFrameCount = Time.frameCount;
            lastTime = Time.realtimeSinceStartup;
            if (frameCount <= 0 || elapsedSec <= 0f)
                continue;

            float fps = frameCount / elapsedSec;
            float frameMs = elapsedSec * 1000f / frameCount;
            m_fpsText.SetText("FPS {0:1} ({1:1}ms)", fps, frameMs);
            ApplyFpsLayout();
        }
    }

    public void SetText(string text)
    {
        if (m_debugText != null)
            m_debugText.text = text;
    }
}
