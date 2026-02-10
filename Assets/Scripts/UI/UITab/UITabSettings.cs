using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using TMPro;


public class UITabSettings : UITabBase
{
    [SerializeField] private Button m_logoutButton;
    [SerializeField] private Button m_testMineralButton;
    [SerializeField] private TMP_Dropdown m_languageDropdown;

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

        if (m_testMineralButton != null)
            m_testMineralButton?.onClick.AddListener(() => DeveloperConsole.ExecuteCommandStatic("addmineral 1000000"));

        InitializeLanguageDropdown();
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
        CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
    }

    public override void OnTabDeactivated()
    {
        CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
    }

    private void OnLogoutButtonClicked()
    {
        UIManager.Instance.ShowConfirmPopup(
            LocalizationManager.Instance.Get("logout"),
            LocalizationManager.Instance.Get("popup_message_logout"),
            new CostStruct(),
            onConfirm: () =>
            {
                NetworkManager.Instance.Logout();
                LoadingManager.LoadSceneWithLoading("MainScene");
            },
            onCancel: null
        );
    }

}

