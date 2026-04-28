// 존 스테이지 버튼 — dot 마커 + 꺾인 연결선 + 레이블(collapsed/expanded)
// 수평선 길이는 현재 활성화된 레이블 뷰의 rect.width를 LateUpdate에서 자동 반영
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIZoneStageButton : MonoBehaviour
{
    [SerializeField] private UIFilledCircle m_dotCircle;
    [SerializeField] private UIConnectLine  m_bentLine;
    [SerializeField] private RectTransform  m_labelAnchor; // 꺾임점(bendPos)에 배치, 수평선 시작

    [Header("Collapsed — 존 이름 버튼")]
    [SerializeField] private GameObject m_collapsedView;
    [SerializeField] private Button     m_collapseButton;
    [SerializeField] private TMP_Text   m_collapsedNameText;

    [Header("Expanded — 상세 정보 + 입장 버튼")]
    [SerializeField] private GameObject m_expandedView;
    [SerializeField] private TMP_Text   m_expandedDetailText;
    [SerializeField] private Button     m_enterButton;

    [Header("상태별 색상")]
    [SerializeField] private Color m_colorNotCleared  = new(1.0f, 0.0f, 0.0f, 0.8f);
    [SerializeField] private Color m_colorCleared = new(0.3f, 0.7f, 0.3f, 0.9f);

    private Camera  m_worldCamera;
    private Vector3 m_worldPos;
    private Vector2 m_diagonalOffset;
    private bool    m_labelAboveLine;
    private bool    m_isExpanded;
    private float   m_cachedLabelWidth = 100f;
    private readonly Vector3[] m_labelCorners = new Vector3[4];

    private RectTransform m_collapsedRT;
    private RectTransform m_expandedRT;
    private UnityEngine.UI.Outline m_outline;

    public ZoneStageConfig ZoneStageConfig { get; private set; }
    public bool IsExpanded => m_isExpanded;

    public void Initialize(ZoneStageConfig config, Vector3 worldPos, System.Action onToggle, System.Action onEnter, EZoneState state, Camera worldCamera)
    {
        ZoneStageConfig  = config;
        m_worldCamera    = worldCamera;
        m_worldPos       = worldPos;
        m_diagonalOffset = config.diagonalOffset;
        m_labelAboveLine = config.labelAboveLine;

        if (m_collapsedView != null) m_collapsedRT = m_collapsedView.GetComponent<RectTransform>();
        if (m_expandedView  != null) m_expandedRT  = m_expandedView.GetComponent<RectTransform>();

        // 레이블 위치: 꺾임점 기준 위(pivot 0,0) 또는 아래(pivot 0,1)
        if (m_labelAnchor != null)
            m_labelAnchor.pivot = m_labelAboveLine ? new Vector2(0f, 0f) : new Vector2(0f, 1f);

        // Outline — collapsed 배경 Image에 부착
        if (m_outline == null && m_collapsedView != null)
        {
            if (m_collapsedView.TryGetComponent<Image>(out var bg))
            {
                m_outline = bg.GetComponent<UnityEngine.UI.Outline>();
                if (m_outline == null)
                    m_outline = bg.gameObject.AddComponent<UnityEngine.UI.Outline>();
                m_outline.effectColor    = new Color(1f, 0.8f, 0.2f, 1f);
                m_outline.effectDistance = new Vector2(4f, -4f);
                m_outline.enabled        = false;
            }
        }

        m_collapseButton.onClick.RemoveAllListeners();
        m_collapseButton.onClick.AddListener(() =>
        {
            if (m_isExpanded) { Collapse(); }
            else              { Expand(); onToggle?.Invoke(); }
        });

        if (m_enterButton != null)
        {
            m_enterButton.onClick.RemoveAllListeners();
            m_enterButton.onClick.AddListener(() => onEnter?.Invoke());
        }

        if (m_collapsedNameText != null) m_collapsedNameText.text = config.zoneName;
        RefreshExpandedText(config);

        Collapse();
        SetState(state);
    }

    private void RefreshExpandedText(ZoneStageConfig config)
    {
        if (m_expandedDetailText == null) return;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(config.zoneName);
        if (string.IsNullOrEmpty(config.zoneDescription) == false)
            sb.AppendLine(config.zoneDescription);
        if (config.mineralClearReward > 0)
            sb.Append($"{CommonUtility.Sprite("crystal-growth")} {CommonUtility.FormatBigNumber(config.mineralClearReward)}");
        m_expandedDetailText.text = sb.ToString().TrimEnd();
    }

    public void Expand()
    {
        m_isExpanded = true;
        if (m_collapsedView != null) m_collapsedView.SetActive(false);
        if (m_expandedView  != null) m_expandedView.SetActive(true);
        RefreshCachedLabelWidth();
    }

    public void Collapse()
    {
        m_isExpanded = false;
        if (m_collapsedView != null) m_collapsedView.SetActive(true);
        if (m_expandedView  != null) m_expandedView.SetActive(false);
        RefreshCachedLabelWidth();
    }

    private void RefreshCachedLabelWidth()
    {
        RectTransform active = m_isExpanded ? m_expandedRT : m_collapsedRT;
        if (active == null) { m_cachedLabelWidth = 100f; return; }
        LayoutRebuilder.ForceRebuildLayoutImmediate(active);
        m_cachedLabelWidth = active.rect.width;
    }

    public void SetState(EZoneState state)
    {
        Color color;
        if (state == EZoneState.Cleared)
            color = m_colorCleared;
        else
            color = m_colorNotCleared;

        if (m_dotCircle != null) m_dotCircle.color = color;
        if (m_bentLine  != null) m_bentLine.color  = color;

        var collapsedBg = m_collapsedView != null ? m_collapsedView.GetComponent<Image>() : null;
        if (collapsedBg != null) collapsedBg.color = color;

        var expandedBg = m_expandedView != null ? m_expandedView.GetComponent<Image>() : null;
        if (expandedBg != null) expandedBg.color = color;

        // m_collapseButton.interactable = state != EZoneState.NotCleared;
        // if (m_enterButton != null)
        //     m_enterButton.interactable = state != EZoneState.NotCleared;
    }

    // SetSelected(true) = Expand, SetSelected(false) = Collapse
    public void SetSelected(bool selected)
    {
        if (selected) Expand();
        else          Collapse();

        if (m_outline != null) m_outline.enabled = selected;
    }

    private void LateUpdate()
    {
        if (m_worldCamera == null) return;

        Vector3 screenPos = m_worldCamera.WorldToScreenPoint(m_worldPos);
        bool behind = screenPos.z < 0f;

        if (m_dotCircle  != null) m_dotCircle.gameObject.SetActive(!behind);
        if (m_bentLine   != null) m_bentLine.gameObject.SetActive(!behind);
        if (m_labelAnchor != null) m_labelAnchor.gameObject.SetActive(!behind);

        if (behind) return;

        Vector2 dotPos  = screenPos;
        Vector2 bendPos = dotPos + m_diagonalOffset;

        if (m_dotCircle   != null) m_dotCircle.rectTransform.position = dotPos;
        if (m_labelAnchor != null) m_labelAnchor.position = bendPos;

        // 레이블을 먼저 배치한 뒤 실제 우측 끝(screen space)을 GetWorldCorners로 읽어 수평선 끝점 결정
        // rect.width는 캔버스 단위라 Canvas scaleFactor != 1이면 스크린 픽셀과 불일치하므로 사용 금지
        RectTransform activeRT = m_isExpanded ? m_expandedRT : m_collapsedRT;
        Vector2 lineEnd;
        if (activeRT != null)
        {
            activeRT.GetWorldCorners(m_labelCorners);
            lineEnd = new Vector2(m_labelCorners[2].x, bendPos.y);
        }
        else
        {
            lineEnd = bendPos + new Vector2(m_cachedLabelWidth, 0f);
        }

        if (m_bentLine != null) m_bentLine.SetBentPoints(dotPos, bendPos, lineEnd);
    }

}
