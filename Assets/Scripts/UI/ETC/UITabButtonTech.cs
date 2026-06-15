// 기지 정보 바 — 기술레벨 요약 표시, 클릭 시 상세 팝업
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UITabButtonTech : MonoBehaviour
{
    [SerializeField] private TMP_Text m_textTechLevel;
    
    private void Start()
    {
        EventManager.Subscribe_TechLevelChanged(OnTechLevelChanged);
        RefreshText();
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_TechLevelChanged(OnTechLevelChanged);
    }

    private void OnTechLevelChanged(int techLevel)
    {
        RefreshText();
    }

    private void RefreshText()
    {
        if (m_textTechLevel == null) return;
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;

        int currentLevel = character.GetTechLevel();
        m_textTechLevel.text = $"Lv.{currentLevel}";
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_textTechLevel.transform.parent as RectTransform);
    }

}
