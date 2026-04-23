// 상단 리소스 패널 - 4종 광물량 실시간 표시 (TMP Sprite Asset 아이콘 사용)
using TMPro;
using UnityEngine;

public class UIResourceBar : MonoBehaviour
{
    public TMP_Text m_resourceText;

    private long m_mineral;

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
        m_mineral = character.GetMineral();
        RefreshText();

        EventManager.Subscribe_MineralChanged(OnMineralChanged);
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_MineralChanged(OnMineralChanged);
    }

    private void RefreshText()
    {
        m_resourceText.text = $"{CommonUtility.Sprite("crystal-growth")} {CommonUtility.FormatBigNumber(m_mineral)}";
    }

    public void OnMineralChanged(long mineral) { m_mineral = mineral; RefreshText(); }
}
