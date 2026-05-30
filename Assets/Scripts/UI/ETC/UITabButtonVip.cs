// VIP 탭 버튼 요약 바 — FRONTIER/ADMIRAL 상태 + 남은 기간 표시
using TMPro;
using UnityEngine;

public class UITabButtonVip : MonoBehaviour
{
    [SerializeField] private TMP_Text m_rankText;    // loc: UIVipStatus_Frontier / UIVipStatus_Admiral
    [SerializeField] private TMP_Text m_expiryText;  // "D-14" — Admiral일 때만 표시

    private void Start()
    {
        EventManager.Subscribe_VipStatusChanged(OnVipStatusChanged);
        Refresh();
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_VipStatusChanged(OnVipStatusChanged);
    }

    private void OnVipStatusChanged()
    {
        Refresh();
    }

    private void Refresh()
    {
        bool isAdmiral = IAPManager.Instance != null && IAPManager.Instance.IsVipActive();

        if (m_rankText != null)
        {
            var loc = LocalizationManager.Instance;
            m_rankText.text = loc.Get(isAdmiral ? "UIVipStatus_Admiral" : "UIVipStatus_Frontier");
        }

        if (m_expiryText != null)
        {
            m_expiryText.gameObject.SetActive(isAdmiral);
            if (isAdmiral == true)
            {
                int days = IAPManager.Instance.GetVipRemainingDays();
                m_expiryText.text = LocalizationManager.Instance.Get("UIVipStatus_ExpiryDays", days);
            }
        }
    }
}
