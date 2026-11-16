// 설정 패널 — 왼쪽 탭(계정/일반/고객지원/정보/개발자)을 누르면 오른쪽에 그 탭의 스크롤뷰만 표시. UIManager가 관리하는 독립 패널(다른 진입 화면과 배타적)
// 로그아웃, 언어/사운드 설정, 구글 계정 연동/해제, 개발자 이메일 문의, 개발자 자원 추가
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using TMPro;


// 왼쪽 탭 버튼과 오른쪽 스크롤뷰 한 쌍 — 탭을 누르면 이 스크롤뷰만 켜짐
[System.Serializable]
public class SettingsTab
{
    public Button     button;
    public GameObject scrollView;
    public string     titleLocKey; // 탭 버튼 라벨 로컬라이즈 키
    public bool       isDevOnly;   // 에디터·개발 빌드에서만 표시
}

public class UIPanelSettings : UIPanelBase
{
    private const string k_contactEmailAddress = "fidforge.thefirst@gmail.com";

    [SerializeField] private TMP_Text m_versionText;
    [SerializeField] private TMP_Text m_panelTitleText;

    [Header("탭")]
    [SerializeField] private SettingsTab[] m_tabs;

    private ButtonGroupSystem m_tabGroup; // 선택된 탭 버튼 색 표시 + 클릭 효과음 — 스크롤뷰 전환은 각 항목의 onSelected가 처리
    private readonly List<SettingsTab> m_visibleTabs = new List<SettingsTab>();

    [Header("고객지원")]
    [SerializeField] private Button m_contactEmailButton;

    [Header("계정")]
    [SerializeField] private TMP_Text m_nameText;
    [SerializeField] private Button m_renameCommanderButton;
    [SerializeField] private Button m_redeemCodeButton;
    [SerializeField] private Button m_googleAccountButton;  // 연동/해제 공용 버튼
    [SerializeField] private TMP_Text m_googleAccountButtonText;
    [SerializeField] private Button m_logoutButton;

    [Header("General")]
    [SerializeField] private TMP_Text m_languageText;
    [SerializeField] private TMP_Dropdown m_languageDropdown;
    [SerializeField] private TMP_Text m_bgmLabelText;
    [SerializeField] private Button m_bgmButton;
    [SerializeField] private Image  m_bgmButtonImage;
    [SerializeField] private Slider m_bgmSlider;
    [SerializeField] private TMP_Text m_fxLabelText;
    [SerializeField] private Button m_fxButton;
    [SerializeField] private Image  m_fxButtonImage;
    [SerializeField] private Slider m_fxSlider;

    private float m_bgmVolumeBeforeMute;
    private float m_fxVolumeBeforeMute;


    [Header("라이센스")]
    [SerializeField] private Button m_licenseButton;

    [Header("개발자 도구")]
    [SerializeField] private GameObject m_devToolPanel;
    
    [SerializeField] private Toggle   m_toggleCommander;
    [SerializeField] private Toggle   m_toggleExploPoint;
    [SerializeField] private Toggle   m_togglePvpPoint;
    [SerializeField] private Toggle   m_toggleAchievementPoint;
    [SerializeField] private Button   m_expPointButton;

    [SerializeField] private Toggle   m_toggleRemoveAd;
    [SerializeField] private Button   m_pvpSeasonEndButton; // 서버 dev 명령 pvpseason end 호출
    [SerializeField] private Button   m_orphanGuestButton;

    [SerializeField] private Toggle   m_toggleCheckZoneCleared; // 켜짐(기본값) = 정상적으로 존 클리어 진행도 검사, 꺼짐 = 검사 건너뜀(테스트용)

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

        if (m_contactEmailButton != null)
            m_contactEmailButton.onClick.AddListener(OnContactEmailButtonClicked);

        if (m_pvpSeasonEndButton != null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            m_pvpSeasonEndButton.onClick.AddListener(OnPvpSeasonEndButtonClicked);
#endif
        }

        if (m_expPointButton != null)
            m_expPointButton.onClick.AddListener(OnExpPointButtonClicked);

        if (m_orphanGuestButton != null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            m_orphanGuestButton.onClick.AddListener(OnOrphanGuestButtonClicked);
#endif
        }

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

        if (m_toggleCheckZoneCleared != null)
        {
            bool checkZoneCleared = PlayerPrefs.GetInt("DevCheckZoneCleared", 1) == 1;
            UIPanelExplorationGrid.s_devSkipZoneLockCheck = (checkZoneCleared == false);
            m_toggleCheckZoneCleared.SetIsOnWithoutNotify(checkZoneCleared);
            m_toggleCheckZoneCleared.onValueChanged.AddListener(on =>
            {
                UIPanelExplorationGrid.s_devSkipZoneLockCheck = (on == false);
                PlayerPrefs.SetInt("DevCheckZoneCleared", on ? 1 : 0);
                PlayerPrefs.Save();
            });
        }

        InitializeTabs();
        InitializeLanguageDropdown();
        InitializeSoundSettings();
        RefreshGoogleLinkUI();
        RefreshStaticLocText();
        RefreshVersionText();
    }

    // 개발 빌드가 아니면 개발자 전용 탭(버튼+스크롤뷰)을 숨기고, 보이는 탭만 버튼 그룹에 등록 — 첫 탭이 기본 선택
    private void InitializeTabs()
    {
        if (m_tabs == null) return;

        bool isDevBuild = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        isDevBuild = true;
#endif

        m_tabGroup = gameObject.AddComponent<ButtonGroupSystem>();
        m_tabGroup.defaultIndex = 0;

        for (int i = 0; i < m_tabs.Length; i++)
        {
            SettingsTab tab = m_tabs[i];
            if (tab == null || tab.button == null) continue;

            bool isVisible = tab.isDevOnly == false || isDevBuild == true;
            tab.button.gameObject.SetActive(isVisible);
            if (tab.scrollView != null)
                tab.scrollView.SetActive(false);
            if (isVisible == false) continue;

            if (string.IsNullOrEmpty(tab.titleLocKey) == false)
                SetButtonLocText(tab.button, tab.titleLocKey);

            SettingsTab capturedTab = tab;
            ButtonGroupItem item = new ButtonGroupItem { button = tab.button };
            item.onSelected = () => ShowTabScrollView(capturedTab);
            m_tabGroup.items.Add(item);
            m_visibleTabs.Add(tab);
        }

        m_tabGroup.Initialize();
    }

    private void ShowTabScrollView(SettingsTab selectedTab)
    {
        for (int i = 0; i < m_visibleTabs.Count; i++)
        {
            SettingsTab tab = m_visibleTabs[i];
            if (tab.scrollView != null)
                tab.scrollView.SetActive(tab == selectedTab);
        }
    }

    // 메일 앱을 열어 개발자 주소로 문의 작성 — 본문 맨 아래에 지휘관 ID/버전/기기 정보를 미리 채워 문의 확인을 돕게 함
    private void OnContactEmailButtonClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);

        string subject = string.Format(LocalizationManager.Instance.Get("UITabSettings_ContactEmailSubject"), Application.productName);
        string body = BuildContactEmailBody();
        string escapedSubject = System.Uri.EscapeDataString(subject); // mailto는 공백을 %20으로 받아야 메일 앱에 그대로 보임('+' 금지)
        string escapedBody = System.Uri.EscapeDataString(body);
        string mailtoUrl = "mailto:" + k_contactEmailAddress + "?subject=" + escapedSubject + "&body=" + escapedBody;
        Application.OpenURL(mailtoUrl);
    }

    private string BuildContactEmailBody()
    {
        Commander commander = DataManager.Instance.m_currentCommander;
        long commanderId = (commander != null && commander.m_commanderInfo != null) ? commander.m_commanderInfo.commanderId : 0;

        StringBuilder sb = new StringBuilder();
        sb.Append(LocalizationManager.Instance.Get("UITabSettings_ContactEmailBodyGuide"));
        sb.Append("\n\n\n\n\n");
        sb.Append("----------\n");
        sb.Append("Commander ID: ").Append(commanderId).Append('\n');
        sb.Append("App Version: ").Append(Application.version).Append('\n');
        sb.Append("Device: ").Append(SystemInfo.deviceModel).Append('\n');
        sb.Append("OS: ").Append(SystemInfo.operatingSystem).Append('\n');
        sb.Append("Language: ").Append(LocalizationManager.Instance.GetCurrentLocaleCode());
        return sb.ToString();
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
        CommonUtility.SetUILocText(m_panelTitleText,     "UI_Settings");
        CommonUtility.SetUILocText(m_languageText,       "UITabSettings_Language");
        CommonUtility.SetUILocText(m_bgmLabelText,       "UI_Bgm");
        CommonUtility.SetUILocText(m_fxLabelText,        "UI_Fx");
        if (m_languageText != null)
            m_languageText.color = CommonUtility.PaletteColor("Text.Dark1");

        // 버튼 라벨
        SetButtonLocText(m_renameCommanderButton, "UITabSettings_NameChange");
        SetButtonLocText(m_redeemCodeButton,      "UITabSettings_RedeemCode");
        SetButtonLocText(m_logoutButton,          "UITabSettings_Logout");
        SetButtonLocText(m_licenseButton,         "UITabSettings_License");
        SetButtonLocText(m_contactEmailButton,    "UITabSettings_ContactEmail");
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
        string levelUp         = (m_toggleCommander        != null && m_toggleCommander.isOn        == true) ? "1"   : "0";
        string exploPoint      = (m_toggleExploPoint       != null && m_toggleExploPoint.isOn       == true) ? "100000" : "0";
        string pvpPoint        = (m_togglePvpPoint         != null && m_togglePvpPoint.isOn         == true) ? "100" : "0";
        string achievementPoint = (m_toggleAchievementPoint != null && m_toggleAchievementPoint.isOn == true) ? "10000" : "0";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        NetworkManager.Instance.ExecuteDevCommand("adddevresources", new string[] { levelUp, exploPoint, pvpPoint, achievementPoint }, OnDevResourcesResponse);
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // 서버 응답("설명|key:value|key:value...")의 각 항목을 로컬 Commander에 반영
    private void OnDevResourcesResponse(ApiResponse<string> response)
    {
        if (response == null || response.errorCode != 0)
        {
            Debug.LogWarning($"adddevresources failed: errorCode={(response != null ? response.errorCode : -1)}");
            return;
        }

        string[] parts = response.data.Split('|');
        for (int i = 1; i < parts.Length; i++)
            ApplyDevResourceValue(parts[i]);
    }

    private void ApplyDevResourceValue(string data)
    {
        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander == null) return;

        string[] keyValue = data.Split(':');
        if (keyValue.Length != 2) return;

        string key = keyValue[0].Trim();
        string value = keyValue[1].Trim();

        switch (key.ToLower())
        {
            case "commanderlevel":
                if (int.TryParse(value, out int commanderLevel))
                    commander.UpdateCommanderLevel(commanderLevel);
                break;
            case "exp":
                if (int.TryParse(value, out int exp))
                    commander.UpdateExp(exp);
                break;
            case "mineralearnedtotal":
                if (int.TryParse(value, out int mineralEarnedTotal))
                    commander.UpdateMineralEarnedTotal(mineralEarnedTotal);
                break;
            case "pvppointmaxgot":
                if (int.TryParse(value, out int pvpMaxGot))
                    commander.UpdatePvpPointMaxGot(pvpMaxGot);
                break;
            case "pvppoint":
                if (int.TryParse(value, out int pvpPoint))
                    commander.UpdatePvpPoint(pvpPoint);
                break;
            case "achievementpoint":
                if (int.TryParse(value, out int achievementPoint))
                    commander.UpdateAchievementPoint(achievementPoint);
                break;
        }
    }

    private void OnPvpSeasonEndButtonClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        NetworkManager.Instance.ExecuteDevCommand("pvpseason", new string[] { "end" }, response =>
        {
            if (response == null) return;
            Debug.Log($"[Dev] pvpseason end: errorCode={response.errorCode} {response.data}");
        });
    }
#endif

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
                    NetworkManager.Instance.SetGuestId(response.data.guestId);
                if (response.data != null && string.IsNullOrEmpty(response.data.guestSecret) == false)
                    NetworkManager.Instance.SetGuestSecret(response.data.guestSecret);
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

    // 서버 계정(DeleteAccount)은 건드리지 않고 로컬 재로그인 자격증명만 삭제 —
    // test.guest-keep-data-on-logout=true 상황(서버 계정은 남는데 로컬엔 재로그인 수단이 없어지는 "고아 게스트 계정")을
    // 서버 설정 변경 없이 재현하기 위한 개발자 전용 버튼
    private void OnOrphanGuestButtonClicked()
    {
        NetworkManager.Instance.Logout();
        DoLocalLogout();
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
