// 기지 정보 바 — 기술레벨 요약 표시, 클릭 시 상세 팝업
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UITabButtonCommander : MonoBehaviour
{
    [SerializeField] private TMP_Text m_textCommanderLevel;
    
    private void Start()
    {
        EventManager.Subscribe_CommanderLevelChanged(OnCommanderLevelChanged);
        RefreshText();
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_CommanderLevelChanged(OnCommanderLevelChanged);
    }

    private void OnCommanderLevelChanged(int commanderLevel)
    {
        RefreshText();
    }

    private void RefreshText()
    {
        if (m_textCommanderLevel == null) return;
        var commander = DataManager.Instance.m_currentCommander;
        if (commander == null) return;

        int currentLevel = commander.GetCommanderLevel();
        m_textCommanderLevel.text = $"Lv.{currentLevel}";
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_textCommanderLevel.transform.parent as RectTransform);
    }

}
