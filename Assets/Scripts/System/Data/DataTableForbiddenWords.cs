// 금지어 테이블: Inspector에서 편집, ExportToJson()으로 서버에 배포
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "DataTableForbiddenWords", menuName = "Custom/DataTableForbiddenWords")]
public class DataTableForbiddenWords : ScriptableObject
{
    public static readonly string[] DefaultBannedWords =
    {
        // 한국어 비속어
        "씨발", "시발", "ㅅㅂ", "개새끼", "개새", "병신", "ㅂㅅ", "지랄", "존나",
        "좆", "보지", "자지", "미친놈", "미친년", "창녀", "걸레", "쌍년", "쌍놈",
        "니애미", "느금마", "엠창", "찐따", "후장", "항문",
        // 영어 비속어
        "fuck", "shit", "bitch", "asshole", "cunt", "bastard",
        "nigger", "nigga", "fag", "faggot", "whore", "slut", "dick", "cock", "pussy",
        // 시스템 예약어
        "empty", "guest", "fidforge",
    };

    [Tooltip("금지어 목록 (대소문자 구분 없이 포함 여부로 체크)")]
    public List<string> bannedWords = new(DefaultBannedWords);

    public void ResetToDefault()
    {
        bannedWords = new List<string>(DefaultBannedWords);
    }

    [SerializeField, TextArea(5, 15)] private string exportedJson = "";

    public string GetExportFileName() => "DataTableForbiddenWords";

    public string ExportToJson()
    {
        var data = new ForbiddenWordsData { bannedWords = bannedWords };
        string json = JsonConvert.SerializeObject(data, Formatting.Indented);
        exportedJson = json;

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif

        return json;
    }

    // 런타임 클라 측 즉각 체크용
    public bool ContainsForbiddenWord(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        string lower = name.ToLower();
        foreach (string word in bannedWords)
        {
            if (lower.Contains(word.ToLower()) == true)
                return true;
        }
        return false;
    }

    [System.Serializable]
    public class ForbiddenWordsData
    {
        public List<string> bannedWords;
    }
}
