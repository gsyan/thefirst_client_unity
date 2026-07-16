// 존 스테이지 버튼 — world 좌표 → screen 위치 고정, 클릭 시 상세 정보 토글
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIZoneStageButton : MonoBehaviour
{
    [SerializeField] private RectTransform       m_rectTransform;
    [SerializeField] private Image               m_myFleetMarker;
    [SerializeField] private Image               m_labelAnchorImage;
    [SerializeField] private TMP_Text            m_detailText;
    [SerializeField] private Transform           m_rewardContainer;
    private RowImageText[]                       m_rewardTexts;
    [SerializeField] private Button              m_enterButton;
    [SerializeField] private Image               m_enterButtonBg;
    [SerializeField] private TMP_Text            m_enterButtonText;
    [SerializeField] private Image               m_lockIcon;

    private Camera  m_worldCamera;
    private Vector3 m_worldPos;
    private bool    m_isExpanded;
    private ZoneStageConfig m_config;
    private System.Action m_onEnter;
    private EZoneState m_currentState;

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
                SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
                if (m_isExpanded == false)
                    onToggle?.Invoke();
                else if (m_currentState != EZoneState.Locked)
                    m_onEnter?.Invoke();
            });
        }

        if (m_lockIcon != null)
        {
            Sprite lockSprite = UISpriteCache.Get("padlock");
            if (lockSprite != null) m_lockIcon.sprite = lockSprite;
            m_lockIcon.gameObject.SetActive(false);
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
            {
                m_rewardTexts[0].SetTextWithString(CommonUtility.FormatBigNumber(config.mineralClearReward));
                m_rewardTexts[0].SetImageColor(CommonUtility.PaletteColor("Mineral"));
                m_rewardTexts[0].SetTextColor(CommonUtility.PaletteColor("Text.Dark1"));
            }
            else
                m_rewardTexts[0].Hide();
        }
        // [1] 경험치 (매 클리어)
        if (m_rewardTexts.Length > 1)
        {
            if (config.expClearReward > 0)
            {
                m_rewardTexts[1].SetTextWithInt(config.expClearReward);
                m_rewardTexts[1].SetTextColor(CommonUtility.PaletteColor("Text.Dark1"));
            }
            else
                m_rewardTexts[1].Hide();
        }
        // [2] 모듈포인트 (최초 클리어 — 클리어 상태면 숨김)
        if (m_rewardTexts.Length > 2)
        {
            if (config.modulePointClearReward > 0 && isCleared == false)
            {
                m_rewardTexts[2].SetTextWithInt(config.modulePointClearReward);
                m_rewardTexts[2].SetImageColor(CommonUtility.PaletteColor("ModulePoint"));
                m_rewardTexts[2].SetTextColor(CommonUtility.PaletteColor("Text.Dark1"));
            }
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
        if (m_detailText != null)           m_detailText.gameObject.SetActive(false);
        if (m_rewardContainer != null)      m_rewardContainer.gameObject.SetActive(false);
        if (m_enterButtonText != null && m_config != null)
            m_enterButtonText.text = m_config.zoneName;
        if (m_labelAnchorImage != null)     m_labelAnchorImage.enabled = false;
        RebuildLayout();
    }

    private void OnEnable()
    {
        //RebuildLayout();
    }

    private void RebuildLayout()
    {
        //Canvas.ForceUpdateCanvases();
        
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_rewardContainer as RectTransform);
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_labelAnchorImage.rectTransform);
    }

    public void SetStateUIZoneStageButton(EZoneState state)
    {
        Color color = CommonUtility.PaletteColor("General");
        if (state == EZoneState.Cleared)
            color = CommonUtility.PaletteColor("Unlocked");
        else if (state == EZoneState.Locked)
            color = CommonUtility.PaletteColor("Zone.Locked");

        m_currentState = state;

        if (m_labelAnchorImage != null) m_labelAnchorImage.color = color;
        if (m_enterButtonBg != null)    m_enterButtonBg.color = color;
        if (m_detailText != null)       m_detailText.color = color;
        if (m_enterButtonText != null)
        {
            m_enterButtonText.color = color;
            m_enterButtonText.gameObject.SetActive(state != EZoneState.Locked);
        }
        if (m_lockIcon != null)
        {
            m_lockIcon.gameObject.SetActive(state == EZoneState.Locked);
            m_lockIcon.color = CommonUtility.PaletteColor("Zone.Locked");
        }
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

    // 연결선이 LabelAnchor 박스 경계(실제로 눈에 보이는 라벨 영역)에 정확히 닿도록 — screen-space 사각 경계 반환
    // ContentSizeFitter/VerticalLayoutGroup 때문에 크기가 방금 바뀌었을 수 있어 즉시 리빌드 후 측정한다
    public Rect GetLabelAnchorScreenRect()
    {
        if (m_labelAnchorImage == null) return default;
        RectTransform labelAnchorRect = m_labelAnchorImage.rectTransform;
        LayoutRebuilder.ForceRebuildLayoutImmediate(labelAnchorRect);

        Vector3[] corners = new Vector3[4];
        labelAnchorRect.GetWorldCorners(corners); // Screen Space Overlay에서는 world corner == screen 좌표
        float xMin = Mathf.Min(corners[0].x, corners[2].x);
        float xMax = Mathf.Max(corners[0].x, corners[2].x);
        float yMin = Mathf.Min(corners[0].y, corners[2].y);
        float yMax = Mathf.Max(corners[0].y, corners[2].y);
        return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
    }
}
