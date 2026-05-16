// 상단 리소스 패널 - mineral / techPoint / modulePoint / pvpPoint 실시간 표시
using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIResourceBar : MonoBehaviour
{
    [SerializeField] private TMP_Text m_textMineralCurrent;
    [SerializeField] private TMP_Text m_textTechPointCurrent;
    [SerializeField] private TMP_Text m_textModulePointCurrent;
    [SerializeField] private TMP_Text m_textModulePointMaxGot;
    [SerializeField] private TMP_Text m_textPvpPointCurrent;
    [SerializeField] private TMP_Text m_textPvpPointMaxGot;
    [SerializeField] private Image    m_imagePvpPointDday;
    [SerializeField] private TMP_Text m_textPvpPointDday;

    private Color m_pvpDdayColorBase;
    private Color m_pvpDdayColorBright;

    private DateTime  m_pvpExpiry;
    private Coroutine m_ddayCoroutine;

    private static readonly WaitForSeconds s_wait1Sec    = new(1f);
    private static readonly WaitForSeconds s_waitFlicker = new(0.05f);

    void Start()
    {
        if (m_textPvpPointDday != null)
        {
            m_pvpDdayColorBase = m_textPvpPointDday.color;
            Color.RGBToHSV(m_pvpDdayColorBase, out float h, out float s, out float v);
            m_pvpDdayColorBright = Color.HSVToRGB(h, s, Mathf.Min(v * 2f, 1f));
        }

        var character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;

        RefreshAll(character);
        EventManager.Subscribe_MineralChanged(OnMineralChanged);
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_MineralChanged(OnMineralChanged);
    }

    private void RefreshAll(Character character)
    {
        var info = character.GetInfo();
        if (info == null) return;

        if (m_textMineralCurrent != null)
            m_textMineralCurrent.text = character.GetMineral().ToString();

        if (m_textTechPointCurrent != null)
            m_textTechPointCurrent.text = character.GetTechPoint().ToString();

        if (m_textModulePointCurrent != null)
            m_textModulePointCurrent.text = character.GetModulePoint().ToString();
        if (m_textModulePointMaxGot != null)
            m_textModulePointMaxGot.text = $"/ {character.GetModulePointMaxGot()}";

        if (m_textPvpPointCurrent != null)
            m_textPvpPointCurrent.text = character.GetPvpPoint().ToString();
        if (m_textPvpPointMaxGot != null)
            m_textPvpPointMaxGot.text = $"/ {character.GetPvpPointMaxGot()}";

        TryParseExpiry(info.pvpPointExpiry, out m_pvpExpiry);

        bool hasPvp = m_pvpExpiry != default && m_pvpExpiry.ToUniversalTime() > DateTime.UtcNow;
        if (m_imagePvpPointDday != null)
        {
            m_imagePvpPointDday.gameObject.SetActive(hasPvp);
            m_textPvpPointDday.gameObject.SetActive(hasPvp);
        }

        if (m_ddayCoroutine != null) StopCoroutine(m_ddayCoroutine);
        if (hasPvp == true)
            m_ddayCoroutine = StartCoroutine(RunDdayUpdate());

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }

    public void OnMineralChanged(long mineral)
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;
        RefreshAll(character);
    }

    private IEnumerator RunDdayUpdate()
    {
        while (true)
        {
            if (m_pvpExpiry != default)
            {
                TimeSpan left = m_pvpExpiry.ToUniversalTime() - DateTime.UtcNow;
                if (left.TotalSeconds <= 0)
                {
                    if (m_imagePvpPointDday != null) m_imagePvpPointDday.gameObject.SetActive(false);
                    if (m_textPvpPointDday  != null) m_textPvpPointDday.gameObject.SetActive(false);
                    m_pvpExpiry = default;
                    yield break;
                }

                if (m_textPvpPointDday != null)
                {
                    m_textPvpPointDday.text = FormatTimeLeft(m_pvpExpiry);
                    if (left.TotalDays < 3)
                    {
                        float t = (Mathf.Sin(Time.time * Mathf.PI * 2f) + 1f) * 0.5f;
                        m_textPvpPointDday.color = Color.Lerp(m_pvpDdayColorBase, m_pvpDdayColorBright, t);
                        yield return s_waitFlicker;
                        continue;
                    }
                    else
                    {
                        m_textPvpPointDday.color = m_pvpDdayColorBase;
                    }
                }
            }
            else
            {
                yield break;
            }

            yield return s_wait1Sec;
        }
    }

    private static bool TryParseExpiry(string iso8601, out DateTime result)
    {
        if (string.IsNullOrEmpty(iso8601) == false)
        {
            if (DateTime.TryParse(iso8601, null, DateTimeStyles.RoundtripKind, out result))
                return true;
        }
        result = default;
        return false;
    }

    private static string FormatTimeLeft(DateTime expireAt)
    {
        TimeSpan left = expireAt.ToUniversalTime() - DateTime.UtcNow;
        if (left.TotalSeconds <= 0)  return "0s";
        if (left.TotalDays >= 30)    return $"{(int)(left.TotalDays / 30)}M";
        if (left.TotalDays >= 1)     return $"{(int)left.TotalDays}D";
        if (left.TotalHours >= 1)    return $"{(int)left.TotalHours}h";
        if (left.TotalMinutes >= 1)  return $"{(int)left.TotalMinutes}m";
        return $"{(int)left.TotalSeconds}s";
    }
}
