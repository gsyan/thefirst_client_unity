using UnityEngine;
using UnityEngine.UI;

// VIP 버튼 얼굴 아이콘 — 버튼은 항상 활성 상태라 탭 열림/닫힘과 무관하게 랭크 아이콘만 갱신
// 상세영역(펼침 UI)은 UITabVip(DetailContainer에 부착)이 TabSystem 표준 탭으로 관리
public class UIVipButton : MonoBehaviour
{
    [SerializeField] private Image m_rankImage;

    private void Awake()
    {
        EventManager.Subscribe_VipStatusChanged(OnVipStatusChanged);
    }

    private void Start()
    {
        RefreshRankIcon();
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_VipStatusChanged(OnVipStatusChanged);
    }

    private void OnVipStatusChanged()
    {
        RefreshRankIcon();
    }

    private void RefreshRankIcon()
    {
        if (m_rankImage == null) return;
        if (IAPManager.Instance == null) return;

        bool isAdmiral = IAPManager.Instance.IsVipActive();
        m_rankImage.sprite = UISpriteCache.Get(isAdmiral ? "rank-3" : "rank-1");
    }
}
