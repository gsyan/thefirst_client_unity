// VIP 상세 팝오버 — 다른 화면(FLEET/COMMANDER 등)과 배타적이지 않은 로컬 팝오버라 UIManager 패널 스택에 얹지 않고,
// UIVipButton이 이 오브젝트의 SetActive를 직접 토글함(자세한 내용은 UIVipButton.cs 참고)
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIVipDetail : MonoBehaviour
{
    [Header("부모 버튼(레이아웃 리빌드용)")]
    [SerializeField] private RectTransform m_buttonRectTransform;

    [Header("상세 영역")]
    [SerializeField] private TMP_Text      m_benefitName;     // TextBenefitName
    [SerializeField] private Image         m_badgeIcon;       // 상단 뱃지 아이콘(rank-3)
    [SerializeField] private RectTransform m_benefitRT;
    private TMP_Text[] m_benefits;

    [SerializeField] private Button     m_purchaseButton;
    [SerializeField] private GameObject m_purchaseButtonParent;
    [SerializeField] private TMP_Text   m_purchaseLabel1;  // TextBecomeAdmiral
    [SerializeField] private TMP_Text   m_purchaseLabel2;  // TextCommanderLevel2

    private RectTransform m_rectTransform;

    private void Awake()
    {
        m_rectTransform = GetComponent<RectTransform>();

        if (m_purchaseButton != null)
            m_purchaseButton.onClick.AddListener(OnPurchaseButtonClicked);
        if (m_benefitRT != null)
        {
            m_benefits = m_benefitRT.GetComponentsInChildren<TMP_Text>(true);
            InitBenefitTexts();
        }

        EventManager.Subscribe_VipStatusChanged(OnVipStatusChanged);
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_VipStatusChanged(OnVipStatusChanged);
    }

    private void OnVipStatusChanged()
    {
        Refresh();
    }

    private void InitBenefitTexts()
    {
        if (m_benefits == null || m_benefits.Length < 3) return;
        if (IAPManager.Instance == null) return;

        var loc = LocalizationManager.Instance;

        string[] texts =
        {
            loc.Get("UIVipStatus_Benefit_NoAds"),
            loc.Get("UIVipStatus_Benefit_Daily"),
            loc.Get("UIVipStatus_Benefit_InstantFleetRestore"),
        };
        for (int i = 0; i < texts.Length; i++)
        {
            if (m_benefits[i] == null) continue;
            m_benefits[i].text = $"• {texts[i]}";
        }
    }

    public void Refresh()
    {
        if (IAPManager.Instance == null) return;

        InitBenefitTexts();

        var loc = LocalizationManager.Instance;
        bool isAdmiral = IAPManager.Instance.IsVipActive();

        if (m_benefitName != null)
        {
            string status = isAdmiral ? loc.Get("UIVipStatus_ExpiryDays", IAPManager.Instance.GetVipRemainingDays()) : loc.Get("UIVipStatus_Expired");
            m_benefitName.text = loc.Get("UIVipStatus_BenefitTitle") + " " + status;
        }

        Color titleColor = isAdmiral
            ? CommonUtility.PaletteColor("Vip")
            : CommonUtility.PaletteColor("VipDark");

        // 혜택 목록은 VipDark 대비 차이가 미미해서 더 어두운 VipDark2 사용
        Color benefitColor = isAdmiral
            ? CommonUtility.PaletteColor("Vip")
            : CommonUtility.PaletteColor("VipDark2");

        if (m_benefitName != null)
            m_benefitName.color = titleColor;

        if (m_badgeIcon != null)
            m_badgeIcon.color = titleColor;

        if (m_benefits != null)
        {
            for (int i = 0; i < m_benefits.Length; i++)
            {
                if (m_benefits[i] == null) continue;
                m_benefits[i].color = benefitColor;
            }
        }

        if (m_purchaseButtonParent != null)
            m_purchaseButtonParent.gameObject.SetActive(isAdmiral == false);

        if (m_purchaseLabel1 != null)
            m_purchaseLabel1.text = loc.Get("UIVipStatus_PurchaseTitle");

        if (m_purchaseLabel2 != null)
        {
            string price        = IAPManager.Instance.GetVipLocalizedPrice();
            string monthDisplay = IAPManager.Instance.GetVipMonthDisplay();
            int    remaining    = IAPManager.Instance.GetMonthRemainingDays();
            m_purchaseLabel2.text = loc.Get("UIVipStatus_PurchasePrice", price, monthDisplay, remaining);
        }

        if (m_benefitName != null) LayoutRebuilder.ForceRebuildLayoutImmediate(m_benefitName.rectTransform);
        if (m_benefitRT != null) LayoutRebuilder.ForceRebuildLayoutImmediate(m_benefitRT);
        if (m_rectTransform != null) LayoutRebuilder.ForceRebuildLayoutImmediate(m_rectTransform);
        if (m_buttonRectTransform != null) LayoutRebuilder.ForceRebuildLayoutImmediate(m_buttonRectTransform);
    }

    private void OnPurchaseButtonClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
#if UNITY_EDITOR
        OnEditorVipPurchaseSimulate();
#else
        if (IAPManager.Instance == null) return;
        if (IAPManager.Instance.IsStoreReady() == false)
        {
            Debug.LogWarning("[UIPanelVip] Store 초기화 안 됨");
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
#endif
    }

#if UNITY_EDITOR
    private void OnEditorVipPurchaseSimulate()
    {
        m_purchaseButton.interactable = false;
        NetworkManager.Instance.DebugForceVip(vipResponse =>
        {
            if (vipResponse == null || vipResponse.errorCode != (int)ServerErrorCode.SUCCESS)
            {
                Debug.LogError($"[UIPanelVip][에디터] VIP 강제 세팅 실패 errorCode={vipResponse?.errorCode}");
                if (m_purchaseButton != null) m_purchaseButton.interactable = true;
                return;
            }

            if (vipResponse.data != null)
                IAPManager.Instance.SetVipExpiry(vipResponse.data.vipExpiry);

            Debug.Log($"[UIPanelVip][에디터] VIP 강제 세팅 완료 expiry={vipResponse.data?.vipExpiry}");

            DailyBonusManager.Instance.TryClaimDailyBonus(claimResult =>
            {
                if (m_purchaseButton != null) m_purchaseButton.interactable = true;

                int granted = claimResult != null ? claimResult.grantedExplorationPoint : 0;
                Debug.Log($"[UIPanelVip][에디터] 일일보상 재청구 결과 granted={granted}");

                Refresh();
            });
        });
    }
#endif
}
