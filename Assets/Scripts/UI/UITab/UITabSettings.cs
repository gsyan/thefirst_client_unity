// 설정 탭 UI — 로그아웃, 언어 설정, 개발자 자원 추가 기능
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using TMPro;


public class UITabSettings : UITabBase
{
    [SerializeField] private Button m_logoutButton;
    [SerializeField] private TMP_Dropdown m_languageDropdown;

    [Header("개발자 도구")]
    [SerializeField] private Button   m_testMineralButton;
    [SerializeField] private Toggle   m_toggleMineral;
    [SerializeField] private Toggle   m_toggleMineralRare;
    [SerializeField] private Toggle   m_toggleMineralExotic;
    [SerializeField] private Toggle   m_toggleMineralDark;

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
            m_testMineralButton.onClick.AddListener(OnTestMineralButtonClicked);

        InitializeLanguageDropdown();
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
        CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
    }

    public override void OnTabDeactivated()
    {
        //CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
    }

    private void OnLogoutButtonClicked()
    {
        UIManager.Instance.ShowConfirmPopup(
            LocalizationManager.Instance.Get("settings_logout"),
            LocalizationManager.Instance.Get("popup_message_logout"),
            null, null, null,
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
