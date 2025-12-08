using UnityEngine;
using System.Collections.Generic;

public class MultiGaugeBar : MonoBehaviour
{
    [SerializeField] private GameObject m_gaugeBarPrefab;
    [SerializeField] private List<GaugeBar> m_gauges = new List<GaugeBar>();
    [SerializeField] private Vector3 m_offsetFromTarget = new Vector3(0, 2f, 0);
    [SerializeField] private float m_smoothSpeed = 5f;
    [SerializeField] private float m_screenMargin = 200f;

    private Transform m_targetTransform;
    private Camera m_mainCamera;
    private Canvas m_canvas;
    private RectTransform m_rectTransform;
    private CanvasGroup m_canvasGroup;

    void Awake()
    {
        m_rectTransform = GetComponent<RectTransform>();
        if (m_rectTransform == null)
            m_rectTransform = gameObject.AddComponent<RectTransform>();

        m_canvasGroup = GetComponent<CanvasGroup>();
        if (m_canvasGroup == null)
            m_canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    void Start()
    {
        InitializeReferences();
    }

    void OnEnable()
    {
        InitializeReferences();
    }

    private void InitializeReferences()
    {
        if (m_mainCamera == null)
            m_mainCamera = Camera.main;
        if (m_canvas == null)
            m_canvas = GetComponentInParent<Canvas>();
    }

    public void SetMultiGaugeTarget(Transform target)
    {
        m_targetTransform = target;
    }


    public void UpdateGauge(int index, float currentValue, float maxValue)
    {
        if (index < 0 || index >= m_gauges.Count) return;

        m_gauges[index].UpdateValue(currentValue, maxValue);
    }

    public void SetGaugeColor(int index, Color color)
    {
        if (index < 0 || index >= m_gauges.Count) return;

        m_gauges[index].SetColor(color);
    }

    public void AddGauge(Color color)
    {
        if (m_gaugeBarPrefab == null)
            m_gaugeBarPrefab = Resources.Load<GameObject>("Prefabs/UI/GaugeBar");
        if (m_gaugeBarPrefab == null) return;

        GameObject gaugeObj = Instantiate(m_gaugeBarPrefab, transform);
        GaugeBar gaugeBar = gaugeObj.GetComponent<GaugeBar>();
        if (gaugeBar == null)
            gaugeBar = gaugeObj.AddComponent<GaugeBar>();

        gaugeBar.InitializeFromPrefab();
        gaugeBar.SetColor(color);
        gaugeBar.SetSmoothSpeed(m_smoothSpeed);
        m_gauges.Add(gaugeBar);
    }

    public void ClearGauges()
    {
        foreach (GaugeBar gauge in m_gauges)
        {
            if (gauge != null)
                Destroy(gauge.gameObject);
        }
        m_gauges.Clear();
    }


    void LateUpdate()
    {
        if (m_targetTransform == null || m_mainCamera == null || m_canvas == null)
            return;

        Vector3 worldPos = m_targetTransform.position + m_offsetFromTarget;
        Vector3 screenPos = m_mainCamera.WorldToScreenPoint(worldPos);

        bool isInFrontOfCamera = screenPos.z > 0;
        bool isInScreenBounds = screenPos.x >= -m_screenMargin && screenPos.x <= Screen.width + m_screenMargin &&
                                screenPos.y >= -m_screenMargin && screenPos.y <= Screen.height + m_screenMargin;

        if (isInFrontOfCamera && isInScreenBounds)
        {
            if (m_canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                m_rectTransform.position = screenPos;
            }
            else if (m_canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                Vector2 localPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    m_canvas.transform as RectTransform,
                    screenPos,
                    m_canvas.worldCamera,
                    out localPos
                );
                m_rectTransform.localPosition = localPos;
            }

            if (m_canvasGroup.alpha == 0)
                m_canvasGroup.alpha = 1;
        }
        else
        {
            if (m_canvasGroup.alpha == 1)
                m_canvasGroup.alpha = 0;
        }
    }


    public void SetOffset(Vector3 offset)
    {
        m_offsetFromTarget = offset;
    }

    public int GetGaugeCount()
    {
        return m_gauges.Count;
    }
}
