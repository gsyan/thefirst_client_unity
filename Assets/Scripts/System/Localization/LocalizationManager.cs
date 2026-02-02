using System;
using System.Collections.Generic;
using UnityEngine;

// 로컬라이제이션 매니저 - CSV 기반 다국어 지원
public class LocalizationManager : MonoSingleton<LocalizationManager>
{
    private const string LANGUAGE_PREF_KEY = "Language";
    private const string LOCALIZATION_PATH = "Localization/Localization_";

    private Dictionary<string, string> m_texts = new Dictionary<string, string>();
    private SystemLanguage m_currentLanguage = SystemLanguage.Korean;

    public SystemLanguage CurrentLanguage => m_currentLanguage;
    public event Action OnLanguageChanged;

    protected override bool ShouldDontDestroyOnLoad => true;

    protected override void OnInitialize()
    {
        // 저장된 언어 설정 로드, 없으면 시스템 언어 사용
        if (PlayerPrefs.HasKey(LANGUAGE_PREF_KEY))
        {
            string savedLang = PlayerPrefs.GetString(LANGUAGE_PREF_KEY);
            if (Enum.TryParse(savedLang, out SystemLanguage lang))
                m_currentLanguage = lang;
        }
        else
        {
            m_currentLanguage = GetSystemLanguageOrDefault();
        }

        LoadLanguage(m_currentLanguage);
    }

    // 텍스트 조회 (키가 없으면 키 그대로 반환)
    public string Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        return m_texts.TryGetValue(key, out string value) ? value : key;
    }

    // 포맷 지원: Get("welcome", playerName) → "환영합니다, {0}님"
    public string Get(string key, params object[] args)
    {
        string value = Get(key);
        if (args == null || args.Length == 0) return value;

        try
        {
            return string.Format(value, args);
        }
        catch
        {
            return value;
        }
    }

    // 언어 변경
    public void SetLanguage(SystemLanguage language)
    {
        if (m_currentLanguage == language) return;

        m_currentLanguage = language;
        LoadLanguage(language);

        PlayerPrefs.SetString(LANGUAGE_PREF_KEY, language.ToString());
        PlayerPrefs.Save();

        OnLanguageChanged?.Invoke();
    }

    // 지원 언어 목록
    public SystemLanguage[] GetSupportedLanguages()
    {
        return new SystemLanguage[]
        {
            SystemLanguage.Korean,
            SystemLanguage.English
        };
    }

    // CSV 로드
    private void LoadLanguage(SystemLanguage language)
    {
        string langCode = GetLanguageCode(language);
        string path = LOCALIZATION_PATH + langCode;

        TextAsset csv = Resources.Load<TextAsset>(path);
        if (csv == null)
        {
            Debug.LogWarning($"[Localization] CSV 파일을 찾을 수 없음: {path}");
            // 폴백: 한국어 로드 시도
            if (language != SystemLanguage.Korean)
            {
                LoadLanguage(SystemLanguage.Korean);
            }
            return;
        }

        ParseCSV(csv.text);
        Debug.Log($"[Localization] 언어 로드 완료: {language} ({m_texts.Count}개 항목)");
    }

    // CSV 파싱 (key,value 형식)
    private void ParseCSV(string csvText)
    {
        m_texts.Clear();

        string[] lines = csvText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            // 빈 줄 또는 주석 스킵
            if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith("//"))
                continue;

            // 헤더 스킵 (첫 줄이 key,value인 경우)
            if (i == 0 && line.ToLower().StartsWith("key,"))
                continue;

            // 첫 번째 콤마로 분리 (value에 콤마가 있을 수 있음)
            int commaIndex = line.IndexOf(',');
            if (commaIndex <= 0) continue;

            string key = line.Substring(0, commaIndex).Trim();
            string value = line.Substring(commaIndex + 1).Trim();

            // 쌍따옴표 제거 (CSV 표준)
            if (value.StartsWith("\"") && value.EndsWith("\""))
            {
                value = value.Substring(1, value.Length - 2);
            }

            // 이스케이프 문자 처리
            value = value.Replace("\\n", "\n");
            value = value.Replace("\"\"", "\"");

            if (!string.IsNullOrEmpty(key))
            {
                m_texts[key] = value;
            }
        }
    }

    // 언어 코드 변환
    private string GetLanguageCode(SystemLanguage language)
    {
        return language switch
        {
            SystemLanguage.Korean => "ko",
            SystemLanguage.English => "en",
            SystemLanguage.Japanese => "ja",
            SystemLanguage.ChineseSimplified => "zh_cn",
            SystemLanguage.ChineseTraditional => "zh_tw",
            _ => "en"
        };
    }

    // 시스템 언어 감지 (지원하지 않는 언어면 영어 기본)
    private SystemLanguage GetSystemLanguageOrDefault()
    {
        SystemLanguage sysLang = Application.systemLanguage;
        return sysLang switch
        {
            SystemLanguage.Korean => SystemLanguage.Korean,
            SystemLanguage.English => SystemLanguage.English,
            _ => SystemLanguage.English
        };
    }

    // 키 존재 여부 확인
    public bool HasKey(string key)
    {
        return m_texts.ContainsKey(key);
    }

    // 디버그: 모든 키 출력
    public void DebugPrintAllKeys()
    {
        Debug.Log($"[Localization] 전체 키 목록 ({m_texts.Count}개):");
        foreach (var key in m_texts.Keys)
        {
            Debug.Log($"  - {key}");
        }
    }
}
