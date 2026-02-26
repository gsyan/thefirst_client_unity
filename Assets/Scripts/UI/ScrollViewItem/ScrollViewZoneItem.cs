// 존 목록 스크롤뷰 아이템 — 상태(진행/클리어) 표시 및 웨이브 진행률 시각화
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum EZoneState
{
    Current,
    Cleared
}

public class ScrollViewZoneItem : MonoBehaviour
{
    [SerializeField] private Button m_enterButton;     // 입장
    [SerializeField] private TMP_Text m_zoneText;
    [SerializeField] private TMP_Text m_zoneStatusText;
    [SerializeField] private Image m_greenImage;

    [Header("상태별 색상")]
    [SerializeField] private Color m_colorSelected = new Color(1f, 0.8f, 0.2f, 1f);
    [SerializeField] private float m_outlineWidth  = 4f;
    
    private UnityEngine.UI.Outline m_outline;
    public ZoneConfig m_zoneConfig { get; private set; }
    private EZoneState m_state;

    public void InitializeScrollViewZoneItem(ZoneConfig zoneConfig, UnityEngine.Events.UnityAction actionEnter, EZoneState state)
    {
        m_zoneConfig = zoneConfig;

        m_enterButton.onClick.RemoveAllListeners();
        m_enterButton.onClick.AddListener(actionEnter);

        m_zoneText.text = zoneConfig.zoneName;

        m_outline = m_enterButton.GetComponent<UnityEngine.UI.Outline>();
        if (m_outline == null)
            m_outline = m_enterButton.gameObject.AddComponent<UnityEngine.UI.Outline>();
        m_outline.effectColor    = m_colorSelected;
        m_outline.effectDistance = new Vector2(m_outlineWidth, -m_outlineWidth);
        m_outline.enabled        = false;

        SetState(state);
    }

    public void SetState(EZoneState state)
    {
        m_state = state;

        if (m_zoneStatusText != null)
        {
            if (state == EZoneState.Cleared)
                CommonUtility.SetUILocText(m_zoneStatusText, "explroation_cleared");
            else
                m_zoneStatusText.text = "";
        }

        if (m_greenImage == null) return;

        // Cleared: 버튼 전체 덮음 / Current: 완전히 숨김 (웨이브 시작 시 SetClearProgress로 채워짐)
        ApplyImageRatio(state == EZoneState.Cleared ? 1f : 0f);
    }

    public void SetSelected(bool selected)
    {
        if (m_outline != null)
            m_outline.enabled = selected;
    }

    // 클리어된 웨이브 수 / 존 클리어 기준으로 진행률 표시
    public void SetClearProgress(int clearedWaves, int zoneClearCount)
    {
        if (m_greenImage == null) return;
        float ratio = zoneClearCount > 0 ? Mathf.Clamp01((float)clearedWaves / zoneClearCount) : 0f;
        ApplyImageRatio(ratio);
    }

    // ShipSelector.ApplyHealthRatio 와 동일한 방식 — 아래서부터 ratio 만큼 채움
    private void ApplyImageRatio(float ratio)
    {
        RectTransform rt = m_greenImage.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(ratio, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
