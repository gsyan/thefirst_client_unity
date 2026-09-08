// AchievementButton 전용 레드닷 — 완료했지만 아직 안 받은 업적이 있으면 표시. UIPanelEntryButton은 범용 컴포넌트라 업적 전용 로직을 안 섞고 별도로 부착
using UnityEngine;

public class UIAchievementButtonBadge : MonoBehaviour
{
    [SerializeField] private GameObject m_redDotImage;

    private void Awake()
    {
        Commander commander = DataManager.Instance.m_currentCommander;
        if (m_redDotImage != null && commander != null)
            m_redDotImage.SetActive(commander.GetHasUnclaimedAchievement());

        EventManager.Subscribe_UnclaimedAchievementChanged(OnUnclaimedAchievementChanged);
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_UnclaimedAchievementChanged(OnUnclaimedAchievementChanged);
    }

    private void OnUnclaimedAchievementChanged(bool hasUnclaimedAchievement)
    {
        if (m_redDotImage != null)
            m_redDotImage.SetActive(hasUnclaimedAchievement);
    }
}
