// 상단 리소스 패널 - 기술레벨 및 4종 광물량 실시간 표시
using TMPro;
using UnityEngine;

public class UIPanelMineral : UIPanelBase
{
    public TMP_Text m_TechLevelText;
    public TMP_Text m_mineralText;
    public TMP_Text m_mineralRareText;
    public TMP_Text m_mineralExoticText;
    public TMP_Text m_mineralDarkText;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (m_TechLevelText == null || m_mineralText == null || m_mineralRareText == null || m_mineralExoticText == null || m_mineralDarkText == null)
        {
            Debug.LogError("Resource text is not assigned");
            return;
        }

        var character = DataManager.Instance.m_currentCharacter;
        if (character == null)
        {
            m_TechLevelText.text = "None";
            m_mineralText.text = "None";
            m_mineralRareText.text = "None";
            m_mineralExoticText.text = "None";
            m_mineralDarkText.text = "None";
            return;
        }
        // 이 패널은 메인 화면에 없기 때문에 메인화면에서 character 세팅될때 이벤트로 OnResourceChanged 가 불리우지 않는다. 처음엔 인위적으로 세팅해줌
        OnTechLevelChanged(character.GetTechLevel());
        OnMineralChanged(character.GetMineral());
        OnMineralRareChanged(character.GetMineralRare());
        OnMineralExoticChanged(character.GetMineralExotic());
        OnMineralDarkChanged(character.GetMineralDark());
        
        EventManager.Subscribe_TechLevelChanged(OnTechLevelChanged);
        EventManager.Subscribe_MineralChanged(OnMineralChanged);
        EventManager.Subscribe_MineralRareChanged(OnMineralRareChanged);
        EventManager.Subscribe_MineralExoticChanged(OnMineralExoticChanged);
        EventManager.Subscribe_MineralDarkChanged(OnMineralDarkChanged);    
    }
    
    private void OnDestroy()
    {
        EventManager.Unsubscribe_TechLevelChanged(OnTechLevelChanged);
        EventManager.Unsubscribe_MineralChanged(OnMineralChanged);
        EventManager.Unsubscribe_MineralRareChanged(OnMineralRareChanged);
        EventManager.Unsubscribe_MineralExoticChanged(OnMineralExoticChanged);
        EventManager.Unsubscribe_MineralDarkChanged(OnMineralDarkChanged);
    }

    public void OnTechLevelChanged(int techLevel)
    {
        m_TechLevelText.text = $"T: {techLevel}";
    }
    public void OnMineralChanged(long mineral)
    {
        m_mineralText.text = $"M: {CommonUtility.FormatBigNumber(mineral)}";
    }
    public void OnMineralRareChanged(long mineralRare)
    {
        m_mineralRareText.text = $"R: {CommonUtility.FormatBigNumber(mineralRare)}";
    }
    public void OnMineralExoticChanged(long mineralExotic)
    {
        m_mineralExoticText.text = $"E: {CommonUtility.FormatBigNumber(mineralExotic)}";
    }
    public void OnMineralDarkChanged(long mineralDark)
    {
        m_mineralDarkText.text = $"D: {CommonUtility.FormatBigNumber(mineralDark)}";
    }
}
