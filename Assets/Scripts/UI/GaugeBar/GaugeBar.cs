using UnityEngine;
using UnityEngine.UI;

public class GaugeBar : MonoBehaviour
{
    [SerializeField] private Image m_backgroundImage;
    [SerializeField] private Image m_fillImage;    
    [SerializeField] private Text m_valueText;

    private Color m_color = Color.green;
    private float m_currentValue;
    private float m_maxValue;
    private float m_targetValue;
    private float m_smoothSpeed = 5f;

    private Transform m_targetTransform;
    private Vector3 m_offsetFromTarget = new Vector3(0, 0f, 0);
    private Camera m_mainCamera;
    private Canvas m_canvas;
    private RectTransform m_rectTransform;

    private float m_screenMargin = 200f;
    private bool m_useAutoBounds = true;
    private float m_additionalOffset = 0.5f; // 오브젝트 경계에서 추가로 띄울 거리


    public void SetColor(Color color)
    {
        m_color = color;
        if (m_fillImage != null)
            m_fillImage.color = color;
    }

    public void SetSmoothSpeed(float speed)
    {
        m_smoothSpeed = speed;
    }

    public void UpdateValue(float currentValue, float maxValue)
    {
        m_targetValue = Mathf.Clamp(currentValue, 0, maxValue);
        m_maxValue = maxValue;
    }

    public void InitializeGaugeBar(Transform target, Vector3 offsetFromTarget, Color color, float smoothSpeed)
    {
        if (m_backgroundImage == null)
            m_backgroundImage = transform.Find("Background")?.GetComponent<Image>();
        if (m_fillImage == null)
            m_fillImage = transform.Find("Fill")?.GetComponent<Image>();
        if (m_valueText == null)
            m_valueText = transform.Find("ValueText")?.GetComponent<Text>();

        m_currentValue = 100f;
        m_maxValue = 100f;
        m_targetValue = 100f;

        if (m_mainCamera == null)
            m_mainCamera = Camera.main;

        if (m_canvas == null)
            m_canvas = GetComponentInParent<Canvas>();

        m_rectTransform = GetComponent<RectTransform>();
        if (m_rectTransform == null)
            m_rectTransform = gameObject.AddComponent<RectTransform>();

        m_targetTransform = target;
        m_offsetFromTarget = offsetFromTarget;
        m_color = color;
        if (m_fillImage != null)
            m_fillImage.color = color;
        m_smoothSpeed = smoothSpeed;
    }

    public void SetAutoBounds(bool useAutoBounds, float additionalOffset = 0.5f)
    {
        m_useAutoBounds = useAutoBounds;
        m_additionalOffset = additionalOffset;
    }

    void Update()
    {
        UpdateSmooth();
    }

    private void UpdateSmooth()
    {
        if (m_fillImage == null) return;

        m_currentValue = Mathf.Lerp(m_currentValue, m_targetValue, Time.deltaTime * m_smoothSpeed);

        float fillAmount = m_maxValue > 0 ? m_currentValue / m_maxValue : 0;
        RectTransform fillRect = m_fillImage.rectTransform;
        fillRect.anchorMax = new Vector2(Mathf.Clamp01(fillAmount), 1);

        if (m_valueText != null)
            m_valueText.text = $"{m_currentValue:F0} / {m_maxValue:F0}";
    }

    void LateUpdate()
    {
        if (m_targetTransform == null || m_mainCamera == null || m_canvas == null)
            return;

        Vector3 worldPos = CalculateWorldPosition();
        Vector3 screenPos = m_mainCamera.WorldToScreenPoint(worldPos);

        if (m_canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            m_rectTransform.position = new Vector3(screenPos.x, screenPos.y, 0f);
        }
        else if (m_canvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                m_canvas.transform as RectTransform,
                screenPos,
                m_canvas.worldCamera,
                out Vector2 localPos
            );
            m_rectTransform.localPosition = new Vector3(localPos.x, localPos.y, 0f);
        }
    }

    // 카메라 viewport가 UI 패널 레이아웃(함선탭 열림 등)으로 화면 전체보다 좁아질 수 있음
    // Screen.width/height 전체를 기준으로 판정하면, 카메라가 실제로 렌더링하지 않는(=Clear Flags가 지워주지 않는) 영역에서도
    // 게이지바가 계속 활성 상태로 남아 이전 프레임 픽셀이 지워지지 않고 잔상으로 남는 문제가 있었음
    // 곡면(엣지) 마진(UIManager.CURVED_EDGE_MARGIN)은 카메라 rect가 아니라 레터박스 UI 바로 가리므로, 카메라 렌더링 여부와 무관하게
    // 여기서 직접 제외해야 컬링이 실제로 동작함 (안 그러면 바 뒤에서 계속 그려지기만 하고 숨겨지지 않음)
    public bool IsInScreenBounds()
    {
        if (m_targetTransform == null || m_mainCamera == null)
            return false;

        Vector3 worldPos = CalculateWorldPosition();
        Vector3 screenPos = m_mainCamera.WorldToScreenPoint(worldPos);

        Rect viewportPixelRect = m_mainCamera.pixelRect;
        float curvedEdgeMarginPixels = UIManager.CURVED_EDGE_MARGIN * Screen.width;

        float xMin = Mathf.Max(viewportPixelRect.xMin - m_screenMargin, curvedEdgeMarginPixels);
        float xMax = Mathf.Min(viewportPixelRect.xMax + m_screenMargin, Screen.width - curvedEdgeMarginPixels);

        bool isInFrontOfCamera = screenPos.z > 0;
        bool isInScreenBounds = screenPos.x >= xMin && screenPos.x <= xMax &&
                                screenPos.y >= viewportPixelRect.yMin - m_screenMargin && screenPos.y <= viewportPixelRect.yMax + m_screenMargin;

        return isInFrontOfCamera && isInScreenBounds;
    }



    private Vector3 CalculateWorldPosition()
    {
        if (!m_useAutoBounds)
            return m_targetTransform.position + m_offsetFromTarget;

        Bounds combinedBounds = CommonUtility.CalculateRendererBounds(m_targetTransform, excludeParticles: true, excludeTrails: true, excludeDisabled: true);
        
        if (combinedBounds.size == Vector3.zero)
            return m_targetTransform.position + m_offsetFromTarget;

        Vector3 cameraToObject = combinedBounds.center - m_mainCamera.transform.position;
        Vector3 directionFromCamera = cameraToObject.normalized;

        Vector3 cameraUp = m_mainCamera.transform.up;

        Vector3[] boundAxes = new Vector3[]
        {
            Vector3.right * combinedBounds.extents.x,
            Vector3.up * combinedBounds.extents.y,
            Vector3.forward * combinedBounds.extents.z
        };

        float maxDot = -1.0f;
        float selectedExtent = 0f;

        foreach (Vector3 axis in boundAxes)
        {
            float dot = Mathf.Abs(Vector3.Dot(cameraUp.normalized, axis.normalized));
            if (dot > maxDot)
            {
                maxDot = dot;
                selectedExtent = axis.magnitude;
            }
        }

        Vector3 offsetDirection = m_offsetFromTarget.y >= 0
            ? cameraUp
            : -cameraUp;

        float totalOffset = selectedExtent + m_additionalOffset + Mathf.Abs(m_offsetFromTarget.y);

        Vector3 offset = offsetDirection * totalOffset;
        offset += m_mainCamera.transform.right * m_offsetFromTarget.x;
        offset += directionFromCamera * m_offsetFromTarget.z;

        return combinedBounds.center + offset;
    }

    public float GetCurrentValue() => m_currentValue;
    public float GetMaxValue() => m_maxValue;
    public Color GetColor() => m_color;



}
