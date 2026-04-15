// 상단 리소스 패널 - 4종 광물량 실시간 표시 (TMP Sprite Asset 아이콘 사용)
using TMPro;
using UnityEngine;

public class UIResourceBar : MonoBehaviour
{
    public TMP_Text m_resourceText;

    private long m_mineral;
    private long m_mineralRare;
    private long m_mineralExotic;
    private long m_mineralDark;

    void Start()
    {
        if (m_resourceText == null)
        {
            Debug.LogError("Resource text is not assigned");
            return;
        }

        var character = DataManager.Instance.m_currentCharacter;
        if (character == null)
        {
            m_resourceText.text = "None";
            return;
        }
        // 이 패널은 메인 화면에서 이벤트로 초기값이 세팅되지 않으므로 직접 초기화
        m_mineral       = character.GetMineral();
        m_mineralRare   = character.GetMineralRare();
        m_mineralExotic = character.GetMineralExotic();
        m_mineralDark   = character.GetMineralDark();
        RefreshText();

        EventManager.Subscribe_MineralChanged(OnMineralChanged);
        EventManager.Subscribe_MineralRareChanged(OnMineralRareChanged);
        EventManager.Subscribe_MineralExoticChanged(OnMineralExoticChanged);
        EventManager.Subscribe_MineralDarkChanged(OnMineralDarkChanged);
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_MineralChanged(OnMineralChanged);
        EventManager.Unsubscribe_MineralRareChanged(OnMineralRareChanged);
        EventManager.Unsubscribe_MineralExoticChanged(OnMineralExoticChanged);
        EventManager.Unsubscribe_MineralDarkChanged(OnMineralDarkChanged);
    }

    private void RefreshText()
    {
        m_resourceText.text =
            $"{CommonUtility.Sprite("crystal-growth")} {CommonUtility.FormatBigNumber(m_mineral)}   " +
            $"{CommonUtility.Sprite("minerals")} {CommonUtility.FormatBigNumber(m_mineralRare)}   " +
            $"{CommonUtility.Sprite("emerald")} {CommonUtility.FormatBigNumber(m_mineralExotic)}   " +
            $"{CommonUtility.Sprite("fire-gem")} {CommonUtility.FormatBigNumber(m_mineralDark)}";
    }

    public void OnMineralChanged(long mineral)       { m_mineral       = mineral;       RefreshText(); }
    public void OnMineralRareChanged(long v)         { m_mineralRare   = v;             RefreshText(); }
    public void OnMineralExoticChanged(long v)       { m_mineralExotic = v;             RefreshText(); }
    public void OnMineralDarkChanged(long v)         { m_mineralDark   = v;             RefreshText(); }
}
