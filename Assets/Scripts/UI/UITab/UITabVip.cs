using TMPro;
using UnityEngine;
using UnityEngine.UI;

// VIP 탭 — Frontier/Admiral 상태 표시, 혜택 안내, 구매
// UITabSettings 패턴 동일: 활성화 시 다른 탭 버튼 숨김, 비활성화 시 복원
public class UITabVip : UITabBase
{
    [Header("상태")]
    [SerializeField] private TMP_Text m_rankText;
    [SerializeField] private TMP_Text m_expiryText;   // Admiral일 때 "D-14"

    [Header("혜택")]
    [SerializeField] private TMP_Text m_benefitText;

    [Header("구매")]
    [SerializeField] private Button m_purchaseButton;
    [SerializeField] private TMP_Text m_purchaseLabel1;  // "ADMIRAL 승급"
    [SerializeField] private TMP_Text m_purchaseLabel2;  // "$4.99 / 30일"

    public override void InitializeUITab()
    {
        if (m_purchaseButton != null)
            m_purchaseButton.onClick.AddListener(OnPurchaseButtonClicked);
    }

    public override void OnTabActivated()
    {
        base.OnTabActivated();
        SetOtherTabsVisible(false, includeSelf: true);
        Refresh();
    }

    public override void OnTabDeactivated()
    {
        base.OnTabDeactivated();
        SetOtherTabsVisible(true, includeSelf: true);
    }

    private void Refresh()
    {
        var loc = LocalizationManager.Instance;
        bool isAdmiral = IAPManager.Instance != null && IAPManager.Instance.IsVipActive();

        if (m_rankText != null)
            m_rankText.text = loc.Get(isAdmiral ? "UIVipStatus_Admiral" : "UIVipStatus_Frontier");

        if (m_expiryText != null)
        {
            m_expiryText.gameObject.SetActive(isAdmiral);
            if (isAdmiral == true)
            {
                int days = IAPManager.Instance.GetVipRemainingDays();
                m_expiryText.text = loc.Get("UIVipStatus_ExpiryDays", days);
            }
        }

        if (m_benefitText != null)
        {
            string benefitKey  = isAdmiral ? "UIVipStatus_BenefitAdmiral" : "UIVipStatus_BenefitFrontier";
            int multiplier     = IAPManager.Instance != null ? IAPManager.Instance.GetMineralRewardMultiplier() : 0;
            int dailyAmount    = IAPManager.Instance != null ? IAPManager.Instance.GetDailyMineralAmount() : 0;
            m_benefitText.text = loc.Get(benefitKey, multiplier, dailyAmount);
        }

        if (m_purchaseButton != null)
            m_purchaseButton.gameObject.SetActive(isAdmiral == false);

        if (m_purchaseLabel1 != null)
            m_purchaseLabel1.text = loc.Get("UIVipStatus_PurchaseTitle");

        if (m_purchaseLabel2 != null)
        {
            string price = IAPManager.Instance != null ? IAPManager.Instance.GetVipLocalizedPrice() : string.Empty;
            m_purchaseLabel2.text = loc.Get("UIVipStatus_PurchasePrice", price);
        }
    }

    private void OnPurchaseButtonClicked()
    {
        if (IAPManager.Instance == null) return;
        if (IAPManager.Instance.IsStoreReady() == false)
        {
            Debug.LogWarning("[UITabVip] Store 초기화 안 됨");
            return;
        }

        m_purchaseButton.interactable = false;
        IAPManager.Instance.PurchaseVip((success, _) =>
        {
            if (m_purchaseButton != null)
                m_purchaseButton.interactable = true;

            if (success == true)
                Refresh();
        });
    }
}
