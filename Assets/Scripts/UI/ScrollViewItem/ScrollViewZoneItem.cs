// 존 목록 스크롤뷰 아이템 — 상태(진행/클리어) 표시 및 웨이브 진행률 시각화
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum EZoneState
{
    Cleared,
    NotCleared,
    Locked, // 이전 스테이지 미클리어로 진입 불가
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
    public string m_zoneName { get; private set; }
    private EZoneState m_state;

    // 함선 시스템 대격변으로 ZoneStageConfig 제거됨 — zoneName 문자열만 표시
    public void InitializeScrollViewZoneItem(string zoneName, UnityEngine.Events.UnityAction actionEnter, EZoneState state)
    {
        m_zoneName = zoneName;

        m_enterButton.onClick.RemoveAllListeners();
        m_enterButton.onClick.AddListener(() => { SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true); actionEnter?.Invoke(); });

        m_zoneText.text = zoneName;

        m_outline = m_enterButton.GetComponent<UnityEngine.UI.Outline>();
        if (m_outline == null)
            m_outline = m_enterButton.gameObject.AddComponent<UnityEngine.UI.Outline>();
        m_outline.effectColor    = m_colorSelected;
        m_outline.effectDistance = new Vector2(m_outlineWidth, -m_outlineWidth);
        m_outline.enabled        = false;

        SetZoneItemState(state);
    }

    public void SetZoneItemState(EZoneState state)
    {
        m_state = state;

        if (m_zoneStatusText != null)
        {
            if (state == EZoneState.Cleared)
                CommonUtility.SetUILocText(m_zoneStatusText, "exploration_cleared");
            else
                m_zoneStatusText.text = "";
        }

        if (m_greenImage == null) return;

        // Cleared: 버튼 전체 덮음 / 그 외: 숨김
        ApplyImageRatio(state == EZoneState.Cleared ? 1f : 0f);
    }

    public void SetSelected(bool selected)
    {
        if (m_outline != null)
            m_outline.enabled = selected;
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
