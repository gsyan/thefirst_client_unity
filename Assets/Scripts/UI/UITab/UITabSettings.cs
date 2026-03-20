// 설정 탭 UI — 로그아웃, 언어 설정, 구글 계정 연동/해제, 개발자 자원 추가 기능
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using TMPro;


public class UITabSettings : UITabBase
{
    [SerializeField] private Button m_logoutButton;
    [SerializeField] private Button m_renameCharacterButton;
    [SerializeField] private TMP_Dropdown m_languageDropdown;

    [Header("계정 연동")]
    [SerializeField] private Button m_googleAccountButton;  // 연동/해제 공용 버튼

    [Header("라이센스")]
    [SerializeField] private Button m_licenseButton;

    [Header("개발자 도구")]
    [SerializeField] private Button   m_testMineralButton;
    [SerializeField] private Toggle   m_toggleMineral;
    [SerializeField] private Toggle   m_toggleMineralRare;
    [SerializeField] private Toggle   m_toggleMineralExotic;
    [SerializeField] private Toggle   m_toggleMineralDark;

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

        if (m_logoutButton != null)
            m_logoutButton.onClick.AddListener(OnLogoutButtonClicked);

        if (m_renameCharacterButton != null)
            m_renameCharacterButton.onClick.AddListener(OnRenameCharacterButtonClicked);

        if (m_googleAccountButton != null)
            m_googleAccountButton.onClick.AddListener(OnGoogleAccountButtonClicked);

        if (m_licenseButton != null)
            m_licenseButton.onClick.AddListener(() => UIManager.Instance.ShowLicensePopup());

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
    }

    private void OnTestMineralButtonClicked()
    {
        string mineral       = (m_toggleMineral       != null && m_toggleMineral.isOn       == true) ? "1000000" : "0";
        string mineralRare   = (m_toggleMineralRare   != null && m_toggleMineralRare.isOn   == true) ? "1000000" : "0";
        string mineralExotic = (m_toggleMineralExotic != null && m_toggleMineralExotic.isOn == true) ? "1000000" : "0";
        string mineralDark   = (m_toggleMineralDark   != null && m_toggleMineralDark.isOn   == true) ? "1000000" : "0";
        DeveloperConsole.ExecuteCommandStatic($"addminerals {mineral} {mineralRare} {mineralExotic} {mineralDark}");
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
        RefreshGoogleLinkUI();
    }

    public override void OnTabDeactivated()
    {
        //CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
    }

    // 연동 상태에 따라 버튼 라벨 갱신
    private void RefreshGoogleLinkUI()
    {
        if (m_googleAccountButton == null) return;
        bool linked = DataManager.Instance.m_isGoogleLinked;
        var label = m_googleAccountButton.GetComponentInChildren<TMP_Text>();
        if (label != null)
            CommonUtility.SetUILocText(label, linked ? "settings_google_unlink" : "settings_google_link");
            //label.text = LocalizationManager.Instance.Get(linked ? "settings_google_unlink" : "settings_google_link");
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
        UIManager.Instance.ShowConfirmPopup(
            LocalizationManager.Instance.Get("settings_google_link"),
            LocalizationManager.Instance.Get("popup_message_google_link"),
            null, null,
            onConfirm: () =>
            {
                NetworkManager.Instance.LinkGoogle((response) =>
                {
                    if ((ServerErrorCode)response.errorCode == ServerErrorCode.SUCCESS)
                    {
                        DataManager.Instance.m_isGoogleLinked = true;
                        RefreshGoogleLinkUI();
                        ShowResultMessage(LocalizationManager.Instance.Get("settings_google_link_success"));
                    }
                    else
                    {
                        ShowResultMessage(ErrorCodeMapping.GetMessage(response.errorCode));
                    }
                });
            },
            onCancel: null
        );
    }

    private void ShowUnlinkGoogleConfirm()
    {
        UIManager.Instance.ShowConfirmPopup(
            LocalizationManager.Instance.Get("settings_google_unlink"),
            LocalizationManager.Instance.Get("popup_message_google_unlink"),
            null, null,
            onConfirm: () =>
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
                        ShowResultMessage(LocalizationManager.Instance.Get("settings_google_unlink_success"));
                    }
                    else
                    {
                        ShowResultMessage(ErrorCodeMapping.GetMessage(response.errorCode));
                    }
                });
            },
            onCancel: null
        );
    }

    private void OnRenameCharacterButtonClicked()
    {
        UIManager.Instance.ShowRenameCharacterPopup();
    }

    private void OnLogoutButtonClicked()
    {
        UIManager.Instance.ShowConfirmPopup(
            LocalizationManager.Instance.Get("settings_logout"),
            LocalizationManager.Instance.Get("popup_message_logout"),
            null, null,
            onConfirm: () =>
            {
                NetworkManager.Instance.Logout();
                EventManager.UnsubscribeAll();
                LoadingManager.LoadSceneWithLoading("MainScene");
            },
            onCancel: null
        );
    }

}
