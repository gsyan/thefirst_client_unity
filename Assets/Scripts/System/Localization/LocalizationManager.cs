using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;


// Unity Localization 패키지 기반 로컬라이제이션 매니저
public class LocalizationManager : MonoSingleton<LocalizationManager>
{
    private const string PREF_LOCALE = "SelectedLocale";
    private const string DEFAULT_TABLE = "UI";

    public event Action OnLanguageChanged;

    protected override bool ShouldDontDestroyOnLoad => true;

    protected override void OnInitialize()
    {
        LoadSavedLocale();
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    protected override void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        base.OnDestroy();
    }

    private void OnLocaleChanged(Locale locale)
    {
        OnLanguageChanged?.Invoke();
    }

    // 키로 로컬라이즈된 문자열 가져오기
    public string Get(string key, string table = DEFAULT_TABLE)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;

        var entry = LocalizationSettings.StringDatabase.GetLocalizedString(table, key);
        return string.IsNullOrEmpty(entry) ? key : entry;
    }

    // 포맷 지원: Get("welcome", playerName)
    public string Get(string key, params object[] args)
    {
        string value = Get(key);
        if (args == null || args.Length == 0) return value;

        try { return string.Format(value, args); }
        catch { return value; }
    }

    // 언어 변경
    public void SetLocale(string localeCode)
    {
        var locale = LocalizationSettings.AvailableLocales.GetLocale(localeCode);
        if (locale == null) return;

        LocalizationSettings.SelectedLocale = locale;
        PlayerPrefs.SetString(PREF_LOCALE, localeCode);
        PlayerPrefs.Save();
    }

    // 현재 로케일 코드
    public string GetCurrentLocaleCode()
    {
        var locale = LocalizationSettings.SelectedLocale;
        return locale != null ? locale.Identifier.Code : "en";
    }

    // 사용 가능한 로케일 목록
    public List<Locale> GetAvailableLocales()
    {
        return LocalizationSettings.AvailableLocales.Locales;
    }

    // 저장된 로케일 복원
    private void LoadSavedLocale()
    {
        string savedCode = PlayerPrefs.GetString(PREF_LOCALE, "");
        if (string.IsNullOrEmpty(savedCode)) return;

        var locale = LocalizationSettings.AvailableLocales.GetLocale(savedCode);
        if (locale != null)
            LocalizationSettings.SelectedLocale = locale;
    }
}
