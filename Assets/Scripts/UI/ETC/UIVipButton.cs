using UnityEngine;
using UnityEngine.UI;

// VIP 버튼 얼굴 아이콘 — 버튼은 항상 활성 상태라 팝오버 열림/닫힘과 무관하게 랭크 아이콘만 갱신
// 상세영역(펼침 UI, DetailContainer의 UIVipDetail)은 다른 화면(FLEET/COMMANDER 등)과 배타적이지 않은 로컬 팝오버라
// UIManager 패널 스택에 얹지 않고 이 버튼이 SetActive를 직접 토글함
public class UIVipButton : MonoBehaviour
{
    [SerializeField] private Image m_rankImage;
    private Button m_button;
    private UIVipDetail m_panelVip;

    private void Awake()
    {
        EventManager.Subscribe_VipStatusChanged(OnVipStatusChanged);

        m_panelVip = GetComponentInChildren<UIVipDetail>(true);
        if (m_panelVip != null)
            m_panelVip.gameObject.SetActive(false);

        m_button = GetComponent<Button>();
        if (m_button != null)
            m_button.onClick.AddListener(OnClickToggleVipDetail);
    }

    private void OnClickToggleVipDetail()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_panelVip == null) return;

        bool isOpen = m_panelVip.gameObject.activeSelf;
        m_panelVip.gameObject.SetActive(isOpen == false);
        if (isOpen == false)
            m_panelVip.Refresh();
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
