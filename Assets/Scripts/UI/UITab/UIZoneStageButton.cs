// 존 스테이지 버튼 — world 좌표 → screen 위치 고정, 클릭 시 상세 정보 토글
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIZoneStageButton : MonoBehaviour
{
    [SerializeField] private RectTransform       m_rectTransform;
    [SerializeField] private Image               m_myFleetMarker;
    [SerializeField] private Image               m_pointMarker;
    [SerializeField] private Image               m_pointMarkerSelected;    
    [SerializeField] private Image               m_labelAnchorImage;
    [SerializeField] private TMP_Text            m_detailText;
    [SerializeField] private Transform           m_rewardContainer;
    private RowImageText[]                       m_rewardTexts;
    [SerializeField] private Button              m_enterButton;
    [SerializeField] private Image               m_enterButtonBg;
    [SerializeField] private TMP_Text            m_enterButtonText;

    [Header("상태별 색상")]
    [SerializeField] private Color m_colorNotCleared = new(1.0f, 0.0f, 0.0f, 0.8f);
    [SerializeField] private Color m_colorCleared    = new(0.3f, 0.7f, 0.3f, 0.9f);

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

        if (m_enterButton != null)
        {
            m_enterButton.onClick.RemoveAllListeners();
            m_enterButton.onClick.AddListener(() =>
            {
                if (m_isExpanded == false)
                    onToggle?.Invoke();
                else
                    m_onEnter?.Invoke();
            });
        }

        CacheRewardTexts();
        RefreshDetailText(config);
        RefreshRewardRows(config, state);
        SetStateUIZoneStageButton(state);
        SetMyFleetMarker(false);
        Collapse();
    }

    private void CacheRewardTexts()
    {
        if (m_rewardContainer == null) return;
        m_rewardTexts = m_rewardContainer.GetComponentsInChildren<RowImageText>(true);
    }

    private void RefreshDetailText(ZoneStageConfig config)
    {
        if (m_detailText == null) return;
        m_detailText.text = $"STAGE {config.zoneName}";
    }

    private void RefreshRewardRows(ZoneStageConfig config, EZoneState state)
    {
        if (m_rewardTexts == null || m_rewardTexts.Length == 0) return;
        bool isCleared = state == EZoneState.Cleared;

        // [0] 미네랄 (매 클리어)
        if (m_rewardTexts.Length > 0)
        {
            if (config.mineralClearReward > 0)
                m_rewardTexts[0].SetTextWithString(CommonUtility.FormatBigNumber(config.mineralClearReward));
            else
                m_rewardTexts[0].Hide();
        }
        // [1] 기술포인트 (최초 클리어 — 클리어 상태면 숨김)
        if (m_rewardTexts.Length > 1)
        {
            if (config.techPointClearReward > 0 && isCleared == false)
                m_rewardTexts[1].SetTextWithInt(config.techPointClearReward);
            else
                m_rewardTexts[1].Hide();
        }
        // [2] 모듈포인트 (최초 클리어 — 클리어 상태면 숨김)
        if (m_rewardTexts.Length > 2)
        {
            if (config.modulePointClearReward > 0 && isCleared == false)
                m_rewardTexts[2].SetTextWithInt(config.modulePointClearReward);
            else
                m_rewardTexts[2].Hide();
        }
    }

    public void RefreshRewardRowsForState(EZoneState state)
    {
        RefreshRewardRows(m_config, state);
    }

    public void Expand()
    {
        m_isExpanded = true;
        if (m_pointMarkerSelected != null)  m_pointMarkerSelected.gameObject.SetActive(true);
        if (m_labelAnchorImage != null)     m_labelAnchorImage.enabled = true;
        if (m_detailText != null)           m_detailText.gameObject.SetActive(true);
        if (m_rewardContainer != null)      m_rewardContainer.gameObject.SetActive(true);
        if (m_enterButtonText != null)
            m_enterButtonText.text = LocalizationManager.Instance.Get("UITabExploration_TryZone");
        RebuildLayout();
    }

    public void Collapse()
    {
        m_isExpanded = false;
        if (m_pointMarkerSelected != null)  m_pointMarkerSelected.gameObject.SetActive(false);
        if (m_detailText != null)           m_detailText.gameObject.SetActive(false);
        if (m_rewardContainer != null)      m_rewardContainer.gameObject.SetActive(false);
        if (m_enterButtonText != null && m_config != null)
            m_enterButtonText.text = m_config.zoneName;
        if (m_labelAnchorImage != null)     m_labelAnchorImage.enabled = false;
    }

    private void OnEnable()
    {
        //RebuildLayout();
    }

    private void RebuildLayout()
    {
        //Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_labelAnchorImage.rectTransform);
    }

    public void SetStateUIZoneStageButton(EZoneState state)
    {
        Color color = state == EZoneState.Cleared ? m_colorCleared : m_colorNotCleared;
        if (m_pointMarker != null)      m_pointMarker.color = color;
        if (m_labelAnchorImage != null) m_labelAnchorImage.color = color;
        if (m_enterButtonBg != null)    m_enterButtonBg.color = color;
    }

    public void SetMyFleetMarker(bool active)
    {
        if (m_myFleetMarker != null) m_myFleetMarker.gameObject.SetActive(active);
    }

    public void SetSelectedUIZoneStageButton(bool selected)
    {
        if (selected)
            Expand();
        else
            Collapse();
    }

    public void UpdateScreenPosition()
    {
        if (m_worldCamera == null || m_rectTransform == null) return;
        Vector3 screenPos = m_worldCamera.WorldToScreenPoint(m_worldPos);
        if (screenPos.z < 0f) return;
        m_rectTransform.position = new Vector3(screenPos.x, screenPos.y, 0f);
    }

    public Vector3 GetScreenPosition() => m_rectTransform != null ? m_rectTransform.position : Vector3.zero;
}
