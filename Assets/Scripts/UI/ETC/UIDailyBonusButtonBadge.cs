// 출석 달력 진입 버튼(UIPanelEntryButton) 전용 레드닷 — 오늘 수령 가능한 출석 보상이 있으면 표시
using UnityEngine;

public class UIDailyBonusButtonBadge : MonoBehaviour
{
    [SerializeField] private GameObject m_redDotImage;

    private void Awake()
    {
        Commander commander = DataManager.Instance.m_currentCommander;
        if (m_redDotImage != null && commander != null)
            m_redDotImage.SetActive(commander.GetHasUnclaimedDailyBonus());

        EventManager.Subscribe_UnclaimedDailyBonusChanged(OnUnclaimedDailyBonusChanged);
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_UnclaimedDailyBonusChanged(OnUnclaimedDailyBonusChanged);
    }

    private void OnUnclaimedDailyBonusChanged(bool hasUnclaimedDailyBonus)
    {
        if (m_redDotImage != null)
            m_redDotImage.SetActive(hasUnclaimedDailyBonus);
    }
}
