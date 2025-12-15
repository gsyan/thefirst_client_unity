using TMPro;
using UnityEngine;

public class UIPanelMoney : UIPanelBase
{
    public TextMeshProUGUI m_moneyText;
    public TextMeshProUGUI m_mineralText;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (m_moneyText == null || m_mineralText == null)
        {
            Debug.LogError("Resource text is not assigned");
            return;
        }

        var character = DataManager.Instance.m_currentCharacter;
        if (character == null)
        {
            m_moneyText.text = "None";
            m_mineralText.text = "None";
            return;
        }
        // 이 패널은 메인 화면에 없기 때문에 메인화면에서 character 세팅될때 이벤트로 OnResourceChanged 가 불리우지 않는다. 처음엔 인위적으로 세팅해줌
        OnMoneyChanged(character.GetMoney());
        OnMineralChanged(character.GetMineral());   
        
        EventManager.Subscribe_MoneyChanged(OnMoneyChanged);
        EventManager.Subscribe_MineralChanged(OnMineralChanged);        
    }
    
    public void OnMoneyChanged(long money)
    {
        m_moneyText.text = $"Money: {money}";
    }

    public void OnMineralChanged(long mineral)
    {
        m_mineralText.text = $"Mineral: {mineral}";
    }

}
