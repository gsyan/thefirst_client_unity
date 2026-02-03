using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScrollViewZoneItem : MonoBehaviour
{
    public Button m_selectButton;
    public TMP_Text m_selectButtonText;
    public TMP_Text m_zoneInfoText;  // Wave 수, 적 함선 수 등 표시용 (Optional)
    public TMP_Text m_statusText;    // 클리어/도전 상태 표시 (Optional)
    public Button m_enterButton;

    public ZoneConfig m_zoneConfig { get; private set; }
    public bool m_isCleared { get; private set; }
    public bool m_isNextChallenge { get; private set; }

    public void InitializeScrollViewZoneItem(ZoneConfig zoneConfig, UnityEngine.Events.UnityAction actionSelect, UnityEngine.Events.UnityAction actionEnter, bool isCleared = false, bool isNextChallenge = false)
    {
        m_zoneConfig = zoneConfig;
        m_isCleared = isCleared;
        m_isNextChallenge = isNextChallenge;

        m_selectButton.gameObject.SetActive(true);
        m_selectButton.onClick.RemoveAllListeners();
        m_selectButton.onClick.AddListener(actionSelect);
        m_selectButtonText.text = zoneConfig.zoneName;

        if (m_zoneInfoText != null)
            m_zoneInfoText.text = $"Wave {zoneConfig.TotalWaveCount} | Ships {zoneConfig.TotalEnemyShipCount}";

        // 클리어/도전 상태 표시
        if (m_statusText != null)
        {
            if (isCleared)
                m_statusText.text = "CLEARED";
            else if (isNextChallenge)
                m_statusText.text = "NEW";
            else
                m_statusText.text = "";
        }

        m_enterButton.onClick.RemoveAllListeners();
        m_enterButton.onClick.AddListener(actionEnter);

        // 초기 상태: 관리 버튼 숨김
        SetSelected_ScrollViewZoneItem(false);
    }

    public void SetSelected_ScrollViewZoneItem(bool selected)
    {
        m_enterButton.gameObject.SetActive(selected);
    }
}
