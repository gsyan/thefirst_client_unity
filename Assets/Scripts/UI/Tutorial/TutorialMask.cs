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

    private void OnFullScreenClick()
    {
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
        // 하이라이팅 중이고 타겟이 있으면 구멍 영역 체크
        if (m_isHighlighting && m_currentTarget != null)
        {
            // 타겟의 월드 코너
            Vector3[] corners = new Vector3[4];
            m_currentTarget.GetWorldCorners(corners);

            // 스크린 좌표로 변환
            Vector2 min = RectTransformUtility.WorldToScreenPoint(m_canvasCamera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(m_canvasCamera, corners[2]);

            // 패딩 적용
            min -= new Vector2(m_highlightPadding, m_highlightPadding);
            max += new Vector2(m_highlightPadding, m_highlightPadding);

            // 구멍 안에 있으면 클릭 통과 (false 반환)
            if (screenPoint.x >= min.x && screenPoint.x <= max.x &&
                screenPoint.y >= min.y && screenPoint.y <= max.y)
            {
                return false;
            }
        }

        // 구멍 밖이면 마스크가 클릭 차단 (true 반환)
        return true;
    }
}
