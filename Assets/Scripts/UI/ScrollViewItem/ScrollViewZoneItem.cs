using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScrollViewZoneItem : MonoBehaviour
{
    [SerializeField] private Button m_enterButton;     // 입장
    [SerializeField] private TMP_Text m_zoneText;
    [SerializeField] private TMP_Text m_zoneStatusText;

    public ZoneConfig m_zoneConfig { get; private set; }
    public bool m_isCleared { get; private set; }
    
    public void InitializeScrollViewZoneItem(ZoneConfig zoneConfig, UnityEngine.Events.UnityAction actionEnter, bool isCleared = false)
    {
        m_zoneConfig = zoneConfig;
        m_isCleared = isCleared;
        
        // if (m_zoneInfoText != null)
        //     m_zoneInfoText.text = $"Wave {zoneConfig.TotalWaveCount} | Ships {zoneConfig.TotalEnemyShipCount}";

        m_enterButton.onClick.RemoveAllListeners();
        m_enterButton.onClick.AddListener(actionEnter);

        m_zoneText.text = zoneConfig.zoneName;

        // 클리어 여부에 따라 버튼 활성화 분기
        if (isCleared == false)
        {
            m_zoneStatusText.text = "CLEARED";
        }
        else
        {
            m_zoneStatusText.text = "";
        }        
    }
}
