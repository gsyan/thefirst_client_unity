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
    private Vector3 m_offsetFromTarget = new Vector3(0, 2f, 0);
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

    public bool IsInScreenBounds()
    {
        if (m_targetTransform == null || m_mainCamera == null)
            return false;

        Vector3 worldPos = CalculateWorldPosition();
        Vector3 screenPos = m_mainCamera.WorldToScreenPoint(worldPos);

        bool isInFrontOfCamera = screenPos.z > 0;
        bool isInScreenBounds = screenPos.x >= -m_screenMargin && screenPos.x <= Screen.width + m_screenMargin &&
                                screenPos.y >= -m_screenMargin && screenPos.y <= Screen.height + m_screenMargin;

        return isInFrontOfCamera && isInScreenBounds;
    }



    private Vector3 CalculateWorldPosition()
    {
        if (!m_useAutoBounds)
            return m_targetTransform.position + m_offsetFromTarget;

        Bounds combinedBounds = CalculateObjectBounds(m_targetTransform);
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

    private Bounds CalculateObjectBounds(Transform target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
            return new Bounds(target.position, Vector3.zero);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }

    public float GetCurrentValue() => m_currentValue;
    public float GetMaxValue() => m_maxValue;
    public Color GetColor() => m_color;



}
