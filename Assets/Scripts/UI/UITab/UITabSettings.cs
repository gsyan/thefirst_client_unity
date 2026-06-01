// 설정 탭 UI — 섹션(계정/일반/기타) 구조, 로그아웃, 언어 설정, 구글 계정 연동/해제, 개발자 자원 추가
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using TMPro;


public class UITabSettings : UITabBase
{
    
    [Header("계정")]
    [SerializeField] private TMP_Text m_sectionAccountText;
    [SerializeField] private TMP_Text m_nameText;
    [SerializeField] private Button m_renameCharacterButton;
    [SerializeField] private Button m_googleAccountButton;  // 연동/해제 공용 버튼
    [SerializeField] private Button m_logoutButton;

    [Header("General")]
    [SerializeField] private TMP_Text m_sectionGeneralText;
    [SerializeField] private TMP_Text m_languageText;
    [SerializeField] private TMP_Dropdown m_languageDropdown;

    [Header("라이센스")]
    [SerializeField] private TMP_Text m_sectionInfolText;
    [SerializeField] private Button m_licenseButton;

    [Header("개발자 도구")]
    [SerializeField] private GameObject m_devToolPanel;
    [SerializeField] private Button   m_devConsoleButton;
    [SerializeField] private Button   m_testMineralButton;    
    [SerializeField] private Toggle   m_toggleMineral;
    [SerializeField] private Toggle   m_toggleTechPoint;
    [SerializeField] private Toggle   m_toggleModulePoint;
    [SerializeField] private Toggle   m_togglePvpPoint;

    [SerializeField] private Toggle   m_toggleRemoveAd;

    private SpaceFleet m_myFleet;
    private List<Locale> m_locales;

    public override void InitializeUITab()
    {
        InitializeUITabSettings();
    }

    private void InitializeUITabSettings()
    {
        if (m_myFleet == null)
            m_myFleet = DataManager.Instance.m_currentCharacter.GetOwnedFleet();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (m_devToolPanel != null) m_devToolPanel.SetActive(true);
#else
        if (m_devToolPanel != null) m_devToolPanel.SetActive(false);
#endif

        if (m_logoutButton != null)
            m_logoutButton.onClick.AddListener(OnLogoutButtonClicked);

        if (m_renameCharacterButton != null)
            m_renameCharacterButton.onClick.AddListener(OnRenameCharacterButtonClicked);

        if (m_googleAccountButton != null)
            m_googleAccountButton.onClick.AddListener(OnGoogleAccountButtonClicked);

        if (m_licenseButton != null)
            m_licenseButton.onClick.AddListener(() => UIManager.Instance.ShowLicensePopup());

        if (m_devConsoleButton != null)
            m_devConsoleButton.onClick.AddListener(() => DeveloperConsole.Instance?.ToggleConsole());

        if (m_testMineralButton != null)
            m_testMineralButton.onClick.AddListener(OnTestMineralButtonClicked);

        if (m_toggleRemoveAd != null)
        {
            AdManager.s_devSkipAd = PlayerPrefs.GetInt("DevSkipAd", 0) == 1;
            m_toggleRemoveAd.SetIsOnWithoutNotify(AdManager.s_devSkipAd);
            m_toggleRemoveAd.onValueChanged.AddListener(on =>
            {
                AdManager.s_devSkipAd = on;
                PlayerPrefs.SetInt("DevSkipAd", on ? 1 : 0);
                PlayerPrefs.Save();
            });
        }

        InitializeLanguageDropdown();
        RefreshGoogleLinkUI();
        RefreshStaticLocText();
    }

    // 섹션 헤더·라벨 등 고정 문자열 로컬라이즈
    private void RefreshStaticLocText()
    {
        CommonUtility.SetUILocText(m_sectionAccountText, "UITabSettings_Account");
        CommonUtility.SetUILocText(m_sectionGeneralText, "UITabSettings_General");
        CommonUtility.SetUILocText(m_sectionInfolText,   "UITabSettings_Info");
        CommonUtility.SetUILocText(m_languageText,       "UITabSettings_Language");

        // 버튼 라벨
        SetButtonLocText(m_renameCharacterButton, "UITabSettings_NameChange");
        SetButtonLocText(m_logoutButton,          "UITabSettings_Logout");
        SetButtonLocText(m_licenseButton,         "UITabSettings_License");
    }

    private void SetButtonLocText(Button btn, string key)
    {
        if (btn == null) return;
        var label = btn.GetComponentInChildren<TMP_Text>();
        if (label != null) CommonUtility.SetUILocText(label, key);
    }

    private void OnTestMineralButtonClicked()
    {
        string mineral       = (m_toggleMineral       != null && m_toggleMineral.isOn       == true) ? "100" : "0";
        string techPoint     = (m_toggleTechPoint     != null && m_toggleTechPoint.isOn     == true) ? "100" : "0";
        string modulePoint   = (m_toggleModulePoint   != null && m_toggleModulePoint.isOn   == true) ? "100" : "0";
        string pvpPoint      = (m_togglePvpPoint      != null && m_togglePvpPoint.isOn      == true) ? "100" : "0";
        DeveloperConsole.ExecuteCommandStatic($"addminerals {mineral} {techPoint} {modulePoint} {pvpPoint}");
    }

    private void InitializeLanguageDropdown()
    {
        if (m_languageDropdown == null) return;

        m_locales = LocalizationManager.Instance.GetAvailableLocales();
        m_languageDropdown.ClearOptions();

        var options = new List<string>();
        string currentCode = LocalizationManager.Instance.GetCurrentLocaleCode();
        int currentIndex = 0;
        for (int i = 0; i < m_locales.Count; i++)
        {
            options.Add(m_locales[i].LocaleName);
            if (m_locales[i].Identifier.Code == currentCode)
                currentIndex = i;
        }

        m_languageDropdown.AddOptions(options);
        m_languageDropdown.SetValueWithoutNotify(currentIndex);
        m_languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
    }

    private void OnLanguageChanged(int index)
    {
        LocalizationManager.Instance.SetLocale(m_locales[index].Identifier.Code);
    }

    public override void OnTabActivated()
    {
        base.OnTabActivated();
        SetOtherTabsVisible(false, includeSelf: true);

        RefreshGoogleLinkUI();
        RefreshNameText();
    }

    private void RefreshNameText()
    {
        if (m_nameText == null) return;
        m_nameText.text = DataManager.Instance.m_currentCharacter?.GetName() ?? string.Empty;
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_nameText.transform.parent as RectTransform);
    }

    public override void OnTabDeactivated()
    {
        base.OnTabDeactivated();
        SetOtherTabsVisible(true, includeSelf: true);
    }

    // 연동 상태에 따라 버튼 라벨 갱신
    private void RefreshGoogleLinkUI()
    {
        if (m_googleAccountButton == null) return;
        bool linked = DataManager.Instance.m_isGoogleLinked;
        var label = m_googleAccountButton.GetComponentInChildren<TMP_Text>();
        if (label != null)
            CommonUtility.SetUILocText(label, linked ? "UITabSettings_GoogleUnlink" : "UITabSettings_GoogleLink");
    }

    private void OnGoogleAccountButtonClicked()
    {
        if (DataManager.Instance.m_isGoogleLinked == true)
            ShowUnlinkGoogleConfirm();
        else
            ShowLinkGoogleConfirm();
    }

    private void ShowLinkGoogleConfirm()
    {
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            title     = LocalizationManager.Instance.Get("UITabSettings_GoogleLink"),
            message   = LocalizationManager.Instance.Get("popup_message_google_link"),
            onConfirm = ExecuteLinkGoogle
        });
    }

    private void ExecuteLinkGoogle()
    {
        NetworkManager.Instance.LinkGoogle((response) =>
        {
            if ((ServerErrorCode)response.errorCode == ServerErrorCode.SUCCESS)
            {
                DataManager.Instance.m_isGoogleLinked = true;
                RefreshGoogleLinkUI();
            }
            else
            {
                ShowErrorMessage(ErrorCodeMapping.GetMessage(response.errorCode));
            }
        });
    }

    private void ShowUnlinkGoogleConfirm()
    {
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            title     = LocalizationManager.Instance.Get("UITabSettings_GoogleUnlink"),
            message   = LocalizationManager.Instance.Get("popup_message_google_unlink"),
            onConfirm = ExecuteUnlinkGoogle
        });
    }

    private void ExecuteUnlinkGoogle()
    {
        NetworkManager.Instance.UnlinkGoogle((response) =>
        {
            if ((ServerErrorCode)response.errorCode == ServerErrorCode.SUCCESS)
            {
                DataManager.Instance.m_isGoogleLinked = false;
                if (string.IsNullOrEmpty(response.data?.guestId) == false)
                {
                    PlayerPrefs.SetString("GuestId", response.data.guestId);
                    PlayerPrefs.Save();
                }
                RefreshGoogleLinkUI();
            }
            else
            {
                ShowErrorMessage(ErrorCodeMapping.GetMessage(response.errorCode));
            }
        });
    }

    private void OnRenameCharacterButtonClicked()
    {
        UIManager.Instance.ShowRenameCharacterPopup(onRenameSuccess: RefreshNameText);
    }

    private void OnLogoutButtonClicked()
    {
        bool isGuest = DataManager.Instance.m_isGoogleLinked == false;
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            title     = LocalizationManager.Instance.Get("UITabSettings_Logout"),
            message   = LocalizationManager.Instance.Get(isGuest
                            ? "popup_message_logout_guest"
                            : "popup_message_logout"),
            onConfirm = isGuest ? (System.Action)ExecuteGuestLogout : ExecuteGoogleLogout
        });
    }

    // 게스트: 서버 계정 삭제 후 로컬 초기화 (GuestId 포함 전부 제거)
    private void ExecuteGuestLogout()
    {
        NetworkManager.Instance.DeleteAccount((response) =>
        {
            if (response.errorCode != 0)
            {
                ShowErrorMessage(ErrorCodeMapping.GetMessage(response.errorCode));
                return;
            }
            NetworkManager.Instance.Logout(); // GuestId + 토큰 정리
            DoLocalLogout();
        });
    }

    // Google 연동: 토큰만 폐기, 서버 데이터 유지
    private void ExecuteGoogleLogout()
    {
        NetworkManager.Instance.Logout();
        DoLocalLogout();
    }

    private void DoLocalLogout()
    {
        EventManager.UnsubscribeAll();
        LoadingManager.LoadSceneWithLoading("MainScene");
    }

}
