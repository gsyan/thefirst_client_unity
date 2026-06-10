using TMPro;
using UnityEngine;
using UnityEngine.UI;

// VIP 버튼 독립 컴포넌트 — Top 버튼 클릭으로 DetailContainer 확장/접기
[RequireComponent(typeof(RectTransform))]
public class UIVipButton : MonoBehaviour
{
    [Header("토글 버튼 영역")]
    [SerializeField] private Button     m_toggleButton;
    [SerializeField] private Image      m_rankImage;
    
    [Header("상세 영역")]
    [SerializeField] private GameObject         m_detailContainer;
    [SerializeField] private TMP_Text           m_benefitName;     // TextBenefitName
    [SerializeField] private RectTransform      m_benefitRT;
    private TMP_Text[] m_benefits;

    [SerializeField] private Button m_purchaseButton;
    [SerializeField] private GameObject m_purchaseButtonParent;
    [SerializeField] private TMP_Text   m_purchaseLabel1;  // TextBecomeAdmiral
    [SerializeField] private TMP_Text   m_purchaseLabel2;  // TextTechLevel2

    private bool          m_isOpen = false;
    private RectTransform m_rectTransform;
    private RectTransform m_detailContainerRT;

    private void Awake()
    {
        m_rectTransform = GetComponent<RectTransform>();
        if (m_detailContainer == null || m_toggleButton == null)
        {
            Debug.LogError($"[UIVipButton] 필수 레퍼런스 미설정: detailContainer={m_detailContainer}, toggleButton={m_toggleButton}", this);
            return;
        }
        m_detailContainerRT = m_detailContainer.GetComponent<RectTransform>();
        m_toggleButton.onClick.AddListener(OnToggleClicked);
        
        if (m_purchaseButton != null)
            m_purchaseButton.onClick.AddListener(OnPurchaseButtonClicked);
        if (m_benefitRT != null)
        {
            m_benefits = m_benefitRT.GetComponentsInChildren<TMP_Text>(true);
            InitBenefitTexts();
        }
        m_detailContainer.SetActive(false);
        EventManager.Subscribe_TabSelectionChanged(OnTabSelectionChanged);
        EventManager.Subscribe_EmptySpaceTapped(OnEmptySpaceTapped);
        EventManager.Subscribe_VipStatusChanged(OnVipStatusChanged);
    }

    private void Start()
    {
        Refresh();
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_TabSelectionChanged(OnTabSelectionChanged);
        EventManager.Unsubscribe_EmptySpaceTapped(OnEmptySpaceTapped);
        EventManager.Unsubscribe_VipStatusChanged(OnVipStatusChanged);
    }

    private void OnVipStatusChanged()
    {
        Refresh();
    }

    private void OnEmptySpaceTapped()
    {
        Close();
    }

    private void OnTabSelectionChanged(string systemName, int tabIndex)
    {
        if (systemName != "UIPanelSpace") return;
        Close();
    }

    private void OnToggleClicked()
    {
        if (m_isOpen == false)
            Open();
        // else
        //     Close();
    }

    public void Open()
    {
        if (m_isOpen == true) return;
        m_isOpen = true;
        EventManager.Trigger_VipButtonOpened();
        m_detailContainer.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        if (m_isOpen == false) return;
        m_isOpen = false;
        m_detailContainer.SetActive(false);
    }

    private void InitBenefitTexts()
    {
        if (m_benefits == null || m_benefits.Length < 3) return;
        if (IAPManager.Instance == null) return;

        var loc = LocalizationManager.Instance;
        int multiplier = IAPManager.Instance.GetMineralRewardMultiplier();

        string[] texts =
        {
            loc.Get("UIVipStatus_Benefit_NoAds"),
            loc.Get("UIVipStatus_Benefit_Mineral", multiplier),
            loc.Get("UIVipStatus_Benefit_Daily"),
        };
        for (int i = 0; i < 3; i++)
        {
            if (m_benefits[i] == null) continue;
            m_benefits[i].text = texts[i];
        }
    }

    private void Refresh()
    {
        if (IAPManager.Instance == null) return;

        InitBenefitTexts();

        var loc = LocalizationManager.Instance;
        bool isAdmiral = IAPManager.Instance.IsVipActive();

        if (m_rankImage != null)
            m_rankImage.sprite = UISpriteCache.Get(isAdmiral ? "rank-3" : "rank-1");

        if (m_benefitName != null)
        {
            string status = isAdmiral ? loc.Get("UIVipStatus_ExpiryDays", IAPManager.Instance.GetVipRemainingDays()) : loc.Get("UIVipStatus_Expired");
            m_benefitName.text = loc.Get("UIVipStatus_BenefitTitle") + " " + status;
        }

        Color benefitColor = isAdmiral
            ? CommonUtility.PaletteColor("GeneralBright1")
            : CommonUtility.PaletteColor("GeneralDark1");

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
        if (m_detailContainerRT != null) LayoutRebuilder.ForceRebuildLayoutImmediate(m_detailContainerRT);
        if (m_rectTransform != null) LayoutRebuilder.ForceRebuildLayoutImmediate(m_rectTransform);
    }

    private void OnPurchaseButtonClicked()
    {
        if (IAPManager.Instance == null) return;
        if (IAPManager.Instance.IsStoreReady() == false)
        {
            Debug.LogWarning("[UIVipButton] Store 초기화 안 됨");
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
