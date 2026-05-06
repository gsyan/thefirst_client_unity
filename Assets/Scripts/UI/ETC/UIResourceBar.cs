// 상단 리소스 패널 - 4종 광물량 실시간 표시
using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIResourceBar : MonoBehaviour
{
    [SerializeField] private TMP_Text m_textMineralCurrent;
    [SerializeField] private TMP_Text m_textMineralMaxGot;
    [SerializeField] private TMP_Text m_textMineralPvpCurrent;
    [SerializeField] private TMP_Text m_textMineralPvpMaxGot;
    [SerializeField] private Image    m_imageMineralPvpDday;
    [SerializeField] private TMP_Text m_textMineralPvpDday;
    [SerializeField] private TMP_Text m_textMineralTempCurrent;
    [SerializeField] private TMP_Text m_textMineralTempMaxGot;
    [SerializeField] private Image    m_imageMineralTempDday;
    [SerializeField] private TMP_Text m_textMineralTempDday;

    [Header("Dday Colors")]
    [SerializeField] private Color m_pvpDdayColorBase   = new(0.502f, 0.196f, 0f);    // #803200
    [SerializeField] private Color m_pvpDdayColorBright = new(1f,     0.392f, 0f);    // #FF6400
    [SerializeField] private Color m_tempDdayColorBase  = new(0.251f, 0.125f, 0.314f);// #402050
    [SerializeField] private Color m_tempDdayColorBright = new(0.502f, 0.251f, 0.565f);// #804090

    private DateTime  m_pvpExpiry;
    private DateTime  m_tempExpiry;
    private Coroutine m_ddayCoroutine;

    private static readonly WaitForSeconds s_wait1Sec    = new(1f);
    private static readonly WaitForSeconds s_waitFlicker = new(0.05f);

    void Start()
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;

        RefreshMinerals(character);
        EventManager.Subscribe_MineralChanged(OnMineralChanged);
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_MineralChanged(OnMineralChanged);
    }

    private void RefreshMinerals(Character character)
    {
        var info = character.GetInfo();
        if (info == null) return;

        if (m_textMineralCurrent != null)
            m_textMineralCurrent.text = character.GetMineral().ToString();
        if (m_textMineralMaxGot != null)
            m_textMineralMaxGot.text = $"/ {character.GetMineralMaxGot()}";

        if (m_textMineralPvpCurrent != null)
            m_textMineralPvpCurrent.text = character.GetPvpMineral().ToString();
        if (m_textMineralPvpMaxGot != null)
            m_textMineralPvpMaxGot.text = $"/ {character.GetPvpMineralMaxGot()}";

        if (m_textMineralTempCurrent != null)
            m_textMineralTempCurrent.text = character.GetTempMineral().ToString();
        if (m_textMineralTempMaxGot != null)
            m_textMineralTempMaxGot.text = $"/ {character.GetTempMineralMaxGot()}";

        TryParseExpiry(info.pvpMineralExpiry, out m_pvpExpiry);
        TryParseExpiry(info.tempMineralExpiry, out m_tempExpiry);

        bool hasPvp  = m_pvpExpiry  != default && m_pvpExpiry.ToUniversalTime()  > DateTime.UtcNow;
        bool hasTemp = m_tempExpiry != default && m_tempExpiry.ToUniversalTime() > DateTime.UtcNow;
        if (m_imageMineralPvpDday != null)
        {
            m_imageMineralPvpDday.gameObject.SetActive(hasPvp);
            m_textMineralPvpDday.gameObject.SetActive(hasPvp);
        }
        if (m_imageMineralTempDday != null)
        {
            m_imageMineralTempDday.gameObject.SetActive(hasTemp);
            m_textMineralTempDday.gameObject.SetActive(hasTemp);
        }

        if (m_ddayCoroutine != null) StopCoroutine(m_ddayCoroutine);
        if (hasPvp == true || hasTemp == true)
            m_ddayCoroutine = StartCoroutine(RunDdayUpdate());

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }

    public void OnMineralChanged(long mineral)
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;
        RefreshMinerals(character);
    }

    // 3일 미만이면 sin 깜박임 + 빠른 갱신, 그 이상이면 1초 갱신. 둘 다 만료되면 종료
    private IEnumerator RunDdayUpdate()
    {
        while (true)
        {
            bool anyActive  = false;
            bool anyFlicker = false;

            if (m_pvpExpiry != default)
            {
                TimeSpan left = m_pvpExpiry.ToUniversalTime() - DateTime.UtcNow;
                if (left.TotalSeconds <= 0)
                {
                    if (m_imageMineralPvpDday != null) m_imageMineralPvpDday.gameObject.SetActive(false);
                    if (m_textMineralPvpDday  != null) m_textMineralPvpDday.gameObject.SetActive(false);
                    m_pvpExpiry = default;
                }
                else
                {
                    if (m_textMineralPvpDday != null)
                    {
                        m_textMineralPvpDday.text = FormatTimeLeft(m_pvpExpiry);
                        if (left.TotalDays < 3)
                        {
                            float t = (Mathf.Sin(Time.time * Mathf.PI * 2f) + 1f) * 0.5f;
                            m_textMineralPvpDday.color = Color.Lerp(m_pvpDdayColorBase, m_pvpDdayColorBright, t);
                            anyFlicker = true;
                        }
                        else
                        {
                            m_textMineralPvpDday.color = m_pvpDdayColorBase;
                        }
                    }
                    anyActive = true;
                }
            }

            if (m_tempExpiry != default)
            {
                TimeSpan left = m_tempExpiry.ToUniversalTime() - DateTime.UtcNow;
                if (left.TotalSeconds <= 0)
                {
                    if (m_imageMineralTempDday != null) m_imageMineralTempDday.gameObject.SetActive(false);
                    if (m_textMineralTempDday  != null) m_textMineralTempDday.gameObject.SetActive(false);
                    m_tempExpiry = default;
                }
                else
                {
                    if (m_textMineralTempDday != null)
                    {
                        m_textMineralTempDday.text = FormatTimeLeft(m_tempExpiry);
                        if (left.TotalDays < 3)
                        {
                            float t = (Mathf.Sin(Time.time * Mathf.PI * 2f) + 1f) * 0.5f;
                            m_textMineralTempDday.color = Color.Lerp(m_tempDdayColorBase, m_tempDdayColorBright, t);
                            anyFlicker = true;
                        }
                        else
                        {
                            m_textMineralTempDday.color = m_tempDdayColorBase;
                        }
                    }
                    anyActive = true;
                }
            }

            if (anyActive == false) yield break;
            yield return anyFlicker == true ? s_waitFlicker : s_wait1Sec;
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
