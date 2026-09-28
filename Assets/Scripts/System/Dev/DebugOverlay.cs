using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DebugOverlay : MonoSingleton<DebugOverlay>
{
    private TMP_Text m_debugText;
    private TMP_Text m_fpsText;
    private Canvas m_canvas;

    // 메인 UI 캔버스(UIPanelSpace 등)와 동일한 CanvasScaler 기준 — 좌측 상단 버튼(anchoredPosition 116,0)과 같은 좌표계로 정렬하기 위함
    private static readonly Vector2 k_referenceResolution = new Vector2(2560, 1440);

    private const float k_fpsSampleIntervalSec = 0.5f;
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

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = k_referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;
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

    // 좌측 상단 버튼 3개(CalandarButton 등, anchoredPosition 116,0 / sizeDelta 100,100) 바로 아래 고정 배치 —
    // Resources/Prefabs/UI/Dev/FpsText 프리팹이 있으면 그 프리팹의 RectTransform/TextMeshProUGUI 값이 우선 적용되고, 이 상수들은 프리팹이 없을 때만 씀
    private static readonly Vector2 k_fpsAnchoredPosition = new Vector2(116, -150);
    private static readonly Vector2 k_fpsSize = new Vector2(700, 80);
    private const float k_fpsFontSize = 40f;
    private const string k_fpsTextPrefabPath = "Prefabs/UI/Dev/FpsText";

    private void CreateFpsUI()
    {
        GameObject prefab = Resources.Load<GameObject>(k_fpsTextPrefabPath);
        if (prefab != null)
        {
            GameObject prefabInstance = Instantiate(prefab, m_canvas.transform, false);
            m_fpsText = prefabInstance.GetComponent<TMP_Text>();
            return;
        }

        GameObject textGO = new GameObject("FpsText");
        textGO.transform.SetParent(m_canvas.transform, false);

        m_fpsText = textGO.AddComponent<TextMeshProUGUI>();
        m_fpsText.color = Color.green;
        m_fpsText.alignment = TextAlignmentOptions.TopLeft;
        m_fpsText.raycastTarget = false;
        m_fpsText.fontSize = k_fpsFontSize;

        RectTransform rt = m_fpsText.rectTransform;
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = k_fpsAnchoredPosition;
        rt.sizeDelta = k_fpsSize;
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
        }
    }

    public void SetText(string text)
    {
        if (m_debugText != null)
            m_debugText.text = text;
    }
}
