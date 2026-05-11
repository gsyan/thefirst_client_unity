// 존 스테이지 버튼 — world 좌표 → screen 위치 고정, 클릭 시 상세 정보 토글
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIZoneStageButton : MonoBehaviour
{
    [SerializeField] private RectTransform       m_rectTransform;
    [SerializeField] private Image               m_pointMarker;   // point marker
    [SerializeField] private Button              m_expendButton;  // 누르면 m_detailText 표시 토글
    [SerializeField] private Image               m_labelAnchor;   // bg
    [SerializeField] private Image               m_borderImage;   // border
    [SerializeField] private TMP_Text            m_detailText;    
    [SerializeField] private Button              m_enterButton;
    [SerializeField] private Image               m_enterButtonBg;
    [SerializeField] private Image               m_enterButtonBorder;
    [SerializeField] private TMP_Text            m_enterButtonText;

    [Header("상태별 색상")]
    [SerializeField] private Color m_colorNotCleared = new(1.0f, 0.0f, 0.0f, 0.8f);
    [SerializeField] private Color m_colorCleared    = new(0.3f, 0.7f, 0.3f, 0.9f);
    [SerializeField] private float m_bgAlpha    = 0.02f;

    private Camera  m_worldCamera;
    private Vector3 m_worldPos;
    private bool    m_isExpanded;
    private ZoneStageConfig m_config;
    private System.Action m_onEnter;

    public ZoneStageConfig ZoneStageConfig { get; private set; }
    public bool IsExpanded => m_isExpanded;

    public void InitializeUIZoneStageButton(ZoneStageConfig config, Vector3 worldPos, System.Action onToggle, System.Action onEnter, EZoneState state, Camera worldCamera)
    {
        ZoneStageConfig = config;
        m_config        = config;
        m_onEnter       = onEnter;
        m_worldCamera   = worldCamera;
        m_worldPos      = worldPos;

        m_expendButton.onClick.RemoveAllListeners();
        m_expendButton.onClick.AddListener(() =>
        {
            if (m_isExpanded) { Collapse(); }
            else              { Expand(); onToggle?.Invoke(); }
        });

        // m_enterButton: 미선택 상태 → expand + onToggle / 선택 상태 → onEnter
        if (m_enterButton != null)
        {
            m_enterButton.onClick.RemoveAllListeners();
            m_enterButton.onClick.AddListener(() =>
            {
                if (m_isExpanded == false)
                {
                    onToggle?.Invoke();
                }
                else
                {
                    m_onEnter?.Invoke();
                }
            });
        }

        RefreshDetailText(config);
        SetStateUIZoneStageButton(state);
        Collapse();
    }

    private void RefreshDetailText(ZoneStageConfig config)
    {
        if (m_detailText == null) return;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(config.zoneName);
        if (string.IsNullOrEmpty(config.zoneDescription) == false)
            sb.AppendLine(config.zoneDescription);
        if (config.mineralClearReward > 0)
            sb.Append($"{CommonUtility.Sprite("crystal-growth")} {CommonUtility.FormatBigNumber(config.mineralClearReward)}");
        m_detailText.text = sb.ToString().TrimEnd();
    }

    public void Expand()
    {
        m_isExpanded = true;
        if (m_detailText != null) m_detailText.gameObject.SetActive(true);
        if (m_enterButtonText != null)
            m_enterButtonText.text = LocalizationManager.Instance.Get("UITabExploration_TryZone");
        RebuildLayout();
        Debug.Log($"Expand {this.name}");
    }

    public void Collapse()
    {
        m_isExpanded = false;
        if (m_detailText != null) m_detailText.gameObject.SetActive(false);
        if (m_enterButtonText != null && m_config != null)
            m_enterButtonText.text = m_config.zoneName;
        RebuildLayout();
        Debug.Log($"Collapse {this.name}");
    }

    private void OnEnable()
    {
        //RebuildLayout();
    }

    private void RebuildLayout()
    {
        //Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_labelAnchor.rectTransform);
    }

    public void SetStateUIZoneStageButton(EZoneState state)
    {
        Color color = state == EZoneState.Cleared ? m_colorCleared : m_colorNotCleared;
        if (m_pointMarker != null) m_pointMarker.color = color;
        
        if (m_labelAnchor != null)
        {
            Color bgColor = color;
            bgColor.a = m_bgAlpha;
            m_labelAnchor.color = bgColor;
            m_enterButtonBg.color = bgColor;
        }
        m_borderImage.color = color;

        m_enterButtonBorder.color = color;
        //if (m_enterButtonText != null) m_enterButtonText.color = color;
    }

    public void SetSelectedUIZoneStageButton(bool selected)
    {
        if (selected) Expand();
        else          Collapse();
    }

    private void LateUpdate()
    {
        if (m_worldCamera == null) return;

        Vector3 screenPos = m_worldCamera.WorldToScreenPoint(m_worldPos);
        if (screenPos.z < 0f) return;

        if (m_rectTransform != null)
            m_rectTransform.position = new Vector3(screenPos.x, screenPos.y, 0f);
    }
}
