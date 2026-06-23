using UnityEngine;
using UnityEngine.UI;

// 튜토리얼 UI 강조 마스크 (쉐이더 기반)
[RequireComponent(typeof(Image))]
public class TutorialMask : MonoBehaviour, ICanvasRaycastFilter
{
    [Header("UI 요소")]
    [SerializeField] private Image m_maskImage;
    [SerializeField] private Button m_fullScreenButton;

    [Header("설정")]
    [SerializeField] private Color m_dimColor = new Color(0, 0, 0, 0.7f);
    [SerializeField] private float m_highlightPadding = 0f;
    [SerializeField] private float m_edgeSoftness = 0.005f;

    private Material m_maskMaterial;
    private System.Action m_onClick;
    private RectTransform m_currentTarget;
    private bool m_isHighlighting;
    private Canvas m_canvas;
    private Camera m_canvasCamera;

    // 쉐이더 프로퍼티 ID (캐싱)
    private static readonly int HoleCenterID = Shader.PropertyToID("_HoleCenter");
    private static readonly int HoleSizeID = Shader.PropertyToID("_HoleSize");
    private static readonly int ColorID = Shader.PropertyToID("_Color");
    private static readonly int EdgeSoftnessID = Shader.PropertyToID("_EdgeSoftness");

    private void Awake()
    {
        if (m_fullScreenButton != null)
            m_fullScreenButton.onClick.AddListener(OnFullScreenClick);

        // 캔버스 캐싱
        m_canvas = GetComponentInParent<Canvas>();
        if (m_canvas != null)
            m_canvasCamera = m_canvas.worldCamera;

        // 머티리얼 생성
        InitMaterial();
    }

    private void InitMaterial()
    {
        if (m_maskImage == null) return;

        Shader shader = Shader.Find("UI/TutorialMask");
        if (shader == null)
        {
            Debug.LogError("TutorialMask shader not found!");
            return;
        }

        m_maskMaterial = new Material(shader);
        m_maskMaterial.SetColor(ColorID, m_dimColor);
        m_maskMaterial.SetFloat(EdgeSoftnessID, m_edgeSoftness);
        m_maskImage.material = m_maskMaterial;

        // 구멍 없이 시작 (전체 어둡게)
        SetHoleOff();
    }

    // 구멍 비활성화 (전체 어둡게)
    private void SetHoleOff()
    {
        if (m_maskMaterial == null) return;
        m_maskMaterial.SetVector(HoleCenterID, new Vector4(-10, -10, 0, 0));
        m_maskMaterial.SetVector(HoleSizeID, Vector4.zero);
    }

    // 전체 어둡게 표시 (구멍 없이) - 스토리 텍스트용
    public void ShowDimOnly()
    {
        m_currentTarget = null;
        m_isHighlighting = false;

        if (m_maskImage != null)
            m_maskImage.gameObject.SetActive(true);

        SetHoleOff();
    }

    // 대상 강조
    public void HighlightTarget(RectTransform target)
    {
        m_currentTarget = target;
        m_isHighlighting = true;

        if (m_maskImage != null)
            m_maskImage.gameObject.SetActive(true);

        UpdateHolePosition();
    }

    // 구멍 위치 업데이트
    private void UpdateHolePosition()
    {
        if (m_currentTarget == null || m_maskMaterial == null) return;

        // 타겟의 월드 코너 가져오기
        Vector3[] corners = new Vector3[4];
        m_currentTarget.GetWorldCorners(corners);

        // 스크린 좌표로 변환
        Vector2 minScreen = WorldToScreenNormalized(corners[0]);
        Vector2 maxScreen = WorldToScreenNormalized(corners[2]);

        // 패딩 적용 (스크린 비율로 변환)
        float paddingX = m_highlightPadding / Screen.width;
        float paddingY = m_highlightPadding / Screen.height;
        minScreen -= new Vector2(paddingX, paddingY);
        maxScreen += new Vector2(paddingX, paddingY);

        // 중심과 크기 계산
        Vector2 center = (minScreen + maxScreen) * 0.5f;
        Vector2 halfSize = (maxScreen - minScreen) * 0.5f;

        m_maskMaterial.SetVector(HoleCenterID, new Vector4(center.x, center.y, 0, 0));
        m_maskMaterial.SetVector(HoleSizeID, new Vector4(halfSize.x, halfSize.y, 0, 0));
    }

    // 월드 좌표를 정규화된 스크린 좌표(0-1)로 변환
    private Vector2 WorldToScreenNormalized(Vector3 worldPos)
    {
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(m_canvasCamera, worldPos);
        return new Vector2(screenPos.x / Screen.width, screenPos.y / Screen.height);
    }

    // 강조 해제
    public void HideHighlight()
    {
        m_isHighlighting = false;
        m_currentTarget = null;
        SetHoleOff();

        if (m_maskImage != null)
            m_maskImage.gameObject.SetActive(false);
    }

    // 클릭 가능 설정
    // clickable=true: 마스크 어디든 클릭하면 onClick 호출
    // clickable=false: 구멍 영역만 클릭 통과, 나머지는 차단
    public void SetClickable(bool clickable, System.Action onClick)
    {
        m_onClick = onClick;

        if (m_fullScreenButton != null)
            m_fullScreenButton.gameObject.SetActive(clickable);

        // 항상 raycastTarget 활성화 (ICanvasRaycastFilter로 구멍 영역 제어)
        if (m_maskImage != null)
            m_maskImage.raycastTarget = true;
    }

    // 화면 상단 영역 통과 설정 (3D 공간 터치용)
    // ratio: 상단에서부터 통과시킬 비율 (0.5 = 상단 50%)
    public void SetTopPassthrough(bool enable, float ratio = 0.5f)
    {
        if (enable && m_maskMaterial != null)
        {
            // 상단 영역에 큰 구멍 뚫기
            ratio = Mathf.Clamp01(ratio);
            float centerY = 1f - (ratio * 0.5f);  // 상단 50% → centerY = 0.75
            float halfHeight = ratio * 0.5f;      // 상단 50% → halfHeight = 0.25
            m_maskMaterial.SetVector(HoleCenterID, new Vector4(0.5f, centerY, 0, 0));
            m_maskMaterial.SetVector(HoleSizeID, new Vector4(0.5f, halfHeight, 0, 0));
            m_isHighlighting = true;
            m_currentTarget = null;

            if (m_maskImage != null)
                m_maskImage.gameObject.SetActive(true);
        }
        else if (!enable)
        {
            SetHoleOff();
        }
    }

    private void OnFullScreenClick()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        m_onClick?.Invoke();
    }

    private void LateUpdate()
    {
        // 타겟이 움직일 경우 위치 업데이트
        if (m_isHighlighting && m_currentTarget != null)
            UpdateHolePosition();
    }

    private void OnDestroy()
    {
        if (m_maskMaterial != null)
            Destroy(m_maskMaterial);
    }

    // ICanvasRaycastFilter: 구멍 영역만 클릭 통과
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!m_isHighlighting) return true;

        // 타겟이 있으면 타겟 기준으로 구멍 영역 체크
        if (m_currentTarget != null)
        {
            Vector3[] corners = new Vector3[4];
            m_currentTarget.GetWorldCorners(corners);

            Vector2 min = RectTransformUtility.WorldToScreenPoint(m_canvasCamera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(m_canvasCamera, corners[2]);

            min -= new Vector2(m_highlightPadding, m_highlightPadding);
            max += new Vector2(m_highlightPadding, m_highlightPadding);

            if (screenPoint.x >= min.x && screenPoint.x <= max.x &&
                screenPoint.y >= min.y && screenPoint.y <= max.y)
            {
                return false;
            }
        }
        else if (m_maskMaterial != null)
        {
            // 타겟 없이 구멍이 설정된 경우 (SetTopPassthrough 등)
            Vector4 center = m_maskMaterial.GetVector(HoleCenterID);
            Vector4 halfSize = m_maskMaterial.GetVector(HoleSizeID);

            // 스크린 좌표를 0-1로 정규화
            float normalizedX = screenPoint.x / Screen.width;
            float normalizedY = screenPoint.y / Screen.height;

            // 구멍 영역 체크
            if (Mathf.Abs(normalizedX - center.x) <= halfSize.x &&
                Mathf.Abs(normalizedY - center.y) <= halfSize.y)
            {
                return false;
            }
        }

        return true;
    }
}
