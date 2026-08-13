// 설정 패널 — 섹션(계정/일반/기타) 구조, 로그아웃, 언어 설정, 구글 계정 연동/해제, 개발자 자원 추가. UIManager가 관리하는 독립 패널(다른 진입 화면과 배타적)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using TMPro;


public class UIPanelSettings : UIPanelBase
{
    [SerializeField] private TMP_Text m_versionText;

    [Header("계정")]
    [SerializeField] private TMP_Text m_sectionAccountText;
    [SerializeField] private TMP_Text m_nameText;
    [SerializeField] private Button m_renameCommanderButton;
    [SerializeField] private Button m_redeemCodeButton;
    [SerializeField] private Button m_googleAccountButton;  // 연동/해제 공용 버튼
    [SerializeField] private TMP_Text m_googleAccountButtonText;
    [SerializeField] private Button m_logoutButton;

    [Header("General")]
    [SerializeField] private TMP_Text m_sectionGeneralText;
    [SerializeField] private TMP_Text m_languageText;
    [SerializeField] private TMP_Dropdown m_languageDropdown;
    [SerializeField] private Button m_bgmButton;
    [SerializeField] private Image  m_bgmButtonImage;
    [SerializeField] private Slider m_bgmSlider;
    [SerializeField] private Button m_fxButton;
    [SerializeField] private Image  m_fxButtonImage;
    [SerializeField] private Slider m_fxSlider;

    private float m_bgmVolumeBeforeMute;
    private float m_fxVolumeBeforeMute;


    [Header("라이센스")]
    [SerializeField] private TMP_Text m_sectionInfolText;
    [SerializeField] private Button m_licenseButton;

    [Header("개발자 도구")]
    [SerializeField] private GameObject m_devToolPanel;
    
    [SerializeField] private Toggle   m_toggleCommander;
    [SerializeField] private Toggle   m_toggleExploPoint;
    [SerializeField] private Toggle   m_togglePvpPoint;
    [SerializeField] private Button   m_expPointButton;

    [SerializeField] private Toggle   m_toggleRemoveAd;
    [SerializeField] private Button   m_devConsoleButton;

    private List<Locale> m_locales;

    public override void InitializeUIPanel()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (m_devToolPanel != null) m_devToolPanel.SetActive(true);
#else
        if (m_devToolPanel != null) m_devToolPanel.SetActive(false);
#endif

        if (m_logoutButton != null)
            m_logoutButton.onClick.AddListener(OnLogoutButtonClicked);

        if (m_renameCommanderButton != null)
            m_renameCommanderButton.onClick.AddListener(OnRenameCommanderButtonClicked);

        if (m_redeemCodeButton != null)
            m_redeemCodeButton.onClick.AddListener(() => UIManager.Instance.ShowRedeemCodePopup());

        if (m_googleAccountButton != null)
            m_googleAccountButton.onClick.AddListener(OnGoogleAccountButtonClicked);

        if (m_licenseButton != null)
            m_licenseButton.onClick.AddListener(() => UIManager.Instance.ShowLicensePopup());

        if (m_devConsoleButton != null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            m_devConsoleButton.onClick.AddListener(() =>
            {
                if (DeveloperConsole.Instance != null)
                    DeveloperConsole.Instance.ToggleConsole();
            });
#endif
        }

        if (m_expPointButton != null)
            m_expPointButton.onClick.AddListener(OnExpPointButtonClicked);

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
        InitializeSoundSettings();
        RefreshGoogleLinkUI();
        RefreshStaticLocText();
        RefreshVersionText();
    }

    private void RefreshVersionText()
    {
        if (m_versionText == null) return;
        m_versionText.text = "ver-" + Application.version;
        m_versionText.color = CommonUtility.PaletteColor("Text.Dark2");
    }

    private void InitializeSoundSettings()
    {
        SoundManager sm = SoundManager.Instance;

        float bgmVol = sm.GetBGMVolume();
        float fxVol  = sm.GetFXVolume();
        m_bgmVolumeBeforeMute = bgmVol;
        m_fxVolumeBeforeMute  = fxVol;

        if (m_bgmSlider != null)
        {
            m_bgmSlider.value = bgmVol;
            m_bgmSlider.onValueChanged.AddListener(OnBgmSliderChanged);
        }
        if (m_fxSlider != null)
        {
            m_fxSlider.value = fxVol;
            m_fxSlider.onValueChanged.AddListener(OnFxSliderChanged);
        }
        if (m_bgmButton != null)
            m_bgmButton.onClick.AddListener(OnBgmMuteToggle);
        if (m_fxButton != null)
            m_fxButton.onClick.AddListener(OnFxMuteToggle);

        RefreshSoundButtonImage(m_bgmButtonImage, bgmVol);
        RefreshSoundButtonImage(m_fxButtonImage,  fxVol);
    }

    private void OnBgmSliderChanged(float value)
    {
        SoundManager.Instance.SetBGMVolume(value);
        if (value > 0f) m_bgmVolumeBeforeMute = value;
        RefreshSoundButtonImage(m_bgmButtonImage, value);
    }

    private void OnFxSliderChanged(float value)
    {
        SoundManager.Instance.SetFXVolume(value);
        if (value > 0f) m_fxVolumeBeforeMute = value;
        RefreshSoundButtonImage(m_fxButtonImage, value);
    }

    private void OnBgmMuteToggle()
    {
        bool isMuted = SoundManager.Instance.GetBGMVolume() <= 0f;
        if (isMuted == true)
        {
            float restore = m_bgmVolumeBeforeMute > 0f ? m_bgmVolumeBeforeMute : 0.7f;
            m_bgmSlider.value = restore;
        }
        else
        {
            m_bgmVolumeBeforeMute = SoundManager.Instance.GetBGMVolume();
            m_bgmSlider.value = 0f;
        }
    }

    private void OnFxMuteToggle()
    {
        bool isMuted = SoundManager.Instance.GetFXVolume() <= 0f;
        if (isMuted == true)
        {
            float restore = m_fxVolumeBeforeMute > 0f ? m_fxVolumeBeforeMute : 1f;
            m_fxSlider.value = restore;
        }
        else
        {
            m_fxVolumeBeforeMute = SoundManager.Instance.GetFXVolume();
            m_fxSlider.value = 0f;
        }
    }

    private void RefreshSoundButtonImage(Image buttonImage, float volume)
    {
        if (buttonImage == null) return;
        string spriteName = volume <= 0f ? "speaker-off" : "speaker";
        Sprite sprite = UISpriteCache.Get(spriteName);
        if (sprite != null) buttonImage.sprite = sprite;
    }

    // 섹션 헤더·라벨 등 고정 문자열 로컬라이즈
    private void RefreshStaticLocText()
    {
        CommonUtility.SetUILocText(m_sectionAccountText, "UITabSettings_Account");
        CommonUtility.SetUILocText(m_sectionGeneralText, "UITabSettings_General");
        CommonUtility.SetUILocText(m_sectionInfolText,   "UITabSettings_Info");
        CommonUtility.SetUILocText(m_languageText,       "UITabSettings_Language");
        if (m_languageText != null)
            m_languageText.color = CommonUtility.PaletteColor("Text.Dark1");

        // 버튼 라벨
        SetButtonLocText(m_renameCommanderButton, "UITabSettings_NameChange");
        SetButtonLocText(m_redeemCodeButton,      "UITabSettings_RedeemCode");
        SetButtonLocText(m_logoutButton,          "UITabSettings_Logout");
        SetButtonLocText(m_licenseButton,         "UITabSettings_License");
    }

    private void SetButtonLocText(Button btn, string key)
    {
        if (btn == null) return;
        var label = btn.GetComponentInChildren<TMP_Text>();
        if (label != null) CommonUtility.SetUILocText(label, key);
    }

    private void OnExpPointButtonClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);

        // 서버 adddevresources 1번째 파라미터는 raw exp가 아닌 "1레벨 증가" 트리거 플래그
        string levelUp     = (m_toggleCommander  != null && m_toggleCommander.isOn  == true) ? "1"   : "0";
        string exploPoint  = (m_toggleExploPoint != null && m_toggleExploPoint.isOn == true) ? "100" : "0";
        string pvpPoint    = (m_togglePvpPoint   != null && m_togglePvpPoint.isOn   == true) ? "100" : "0";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DeveloperConsole.ExecuteCommandStatic($"adddevresources {levelUp} {exploPoint} {pvpPoint}");
#endif
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
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        LocalizationManager.Instance.SetLocale(m_locales[index].Identifier.Code);
    }

    public override void OnShowUIPanel()
    {
        RefreshGoogleLinkUI();
        RefreshNameText();
    }

    private void RefreshNameText()
    {
        if (m_nameText == null) return;
        Commander currentCommander = DataManager.Instance.m_currentCommander;
        m_nameText.text = (currentCommander != null) ? currentCommander.GetName() : string.Empty;
        m_nameText.color = CommonUtility.PaletteColor("Text.Dark1");
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_nameText.transform.parent as RectTransform);
    }

    // 연동 상태에 따라 버튼 라벨 갱신
    private void RefreshGoogleLinkUI()
    {
        if (m_googleAccountButton == null) return;
        bool linked = DataManager.Instance.m_isGoogleLinked;
        if (m_googleAccountButtonText != null)
            CommonUtility.SetUILocText(m_googleAccountButtonText, linked ? "UITabSettings_GoogleUnlink" : "UITabSettings_GoogleLink");
    }

    private void OnGoogleAccountButtonClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (DataManager.Instance.m_isGoogleLinked == true)
            ShowUnlinkGoogleConfirm();
        else
            ShowLinkGoogleConfirm();
    }

    private void ShowLinkGoogleConfirm()
    {
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            message   = LocalizationManager.Instance.Get("UIPopupMessage_GoogleLink"),
            onConfirm = ExecuteLinkGoogle,
            onCancel  = () => { }
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
            message   = LocalizationManager.Instance.Get("UIPopupMessage_GoogleUnlink"),
            onConfirm = ExecuteUnlinkGoogle,
            onCancel  = () => { }
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

    private void OnRenameCommanderButtonClicked()
    {
        UIManager.Instance.ShowRenameCommanderPopup(onRenameSuccess: RefreshNameText);
    }

    private void OnLogoutButtonClicked()
    {
        bool isGuest = DataManager.Instance.m_isGoogleLinked == false;
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            message   = LocalizationManager.Instance.Get(isGuest
                            ? "UIPopupMessage_LogoutGuest"
                            : "UIPopupMessage_Logout"),
            onConfirm = isGuest ? (System.Action)ExecuteGuestLogout : ExecuteGoogleLogout,
            onCancel  = () => { }
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

    // Google 연동: 서버 세션 폐기 + 토큰 정리, 서버 데이터 유지
    private void ExecuteGoogleLogout()
    {
        NetworkManager.Instance.LogoutFromServer(DoLocalLogout);
    }

    private void DoLocalLogout()
    {
        EventManager.UnsubscribeAll();
        LoadingManager.LoadSceneWithLoading("MainScene");
    }

    private void ShowErrorMessage(string message)
    {
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig { message = message, autoCloseSec = 5f });
    }

}
