using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum EZoneState
{
    Locked,
    Current,
    Cleared
}

public class ScrollViewZoneItem : MonoBehaviour
{
    [SerializeField] private Button m_enterButton;     // 입장
    [SerializeField] private TMP_Text m_zoneText;
    [SerializeField] private TMP_Text m_zoneStatusText;
    [SerializeField] private Image m_backgroundImage;

    [Header("상태별 색상")]
    [SerializeField] private Color m_colorLocked = new Color(0.3f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color m_colorCurrent = new Color(1f, 0.8f, 0.2f, 1f);
    [SerializeField] private Color m_colorCleared = new Color(0.2f, 0.8f, 0.4f, 1f);

    public ZoneConfig m_zoneConfig { get; private set; }
    private EZoneState m_state;

    public void InitializeScrollViewZoneItem(ZoneConfig zoneConfig, UnityEngine.Events.UnityAction actionEnter, EZoneState state)
    {
        m_zoneConfig = zoneConfig;

        m_enterButton.onClick.RemoveAllListeners();
        m_enterButton.onClick.AddListener(actionEnter);

        m_zoneText.text = zoneConfig.zoneName;

        SetState(state);
    }

    public void SetState(EZoneState state)
    {
        m_state = state;

        // Locked 존은 입장 불가
        if (m_enterButton != null)
            m_enterButton.interactable = state != EZoneState.Locked;

        if (m_zoneStatusText != null)
        {
            if(state == EZoneState.Cleared)
                CommonUtility.SetUILocText(m_zoneStatusText, "explroation_cleared");
            else
                m_zoneStatusText.text = "";
        }
            

        if (m_backgroundImage == null) return;

        switch (state)
        {
            case EZoneState.Locked:
                m_backgroundImage.color = m_colorLocked;
                break;
            case EZoneState.Current:
                m_backgroundImage.color = m_colorCurrent;
                break;
            case EZoneState.Cleared:
                m_backgroundImage.color = m_colorCleared;
                break;
        }
    }
}
