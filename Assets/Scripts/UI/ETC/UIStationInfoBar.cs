// 기지 정보 바 — 기술레벨 요약 표시, 클릭 시 상세 팝업
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIStationInfoBar : MonoBehaviour
{
    [SerializeField] private TMP_Text m_textInfo;
    [SerializeField] private Button   m_btnInfo;

    private void Start()
    {
        if (m_btnInfo != null)
            m_btnInfo.onClick.AddListener(OnInfoClicked);

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
        if (m_textInfo == null) return;
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;

        int currentLevel = character.GetTechLevel();
        m_textInfo.text = $"{CommonUtility.Sprite("gears")} {currentLevel}";
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_textInfo.transform.parent as RectTransform);
    }

    private void OnInfoClicked()
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;

        int currentLevel = character.GetTechLevel();
        int storageCap   = 3 + (currentLevel / 2);
        int maxShips     = DataManager.Instance.m_dataTableConfig.gameSettings.GetMaxShipsAtTechLevel(currentLevel);
        TechLevelResearchData nextNode = GetNextTechLevelNode(character);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{CommonUtility.Sprite("gears")} {currentLevel}  {CommonUtility.Sprite("clockwork")} {storageCap}h  {CommonUtility.Sprite("spiky-field")} {maxShips}");

        if (nextNode != null)
        {
            int nextCap      = 3 + (nextNode.targetTechLevel / 2);
            int nextMaxShips = DataManager.Instance.m_dataTableConfig.gameSettings.GetMaxShipsAtTechLevel(nextNode.targetTechLevel);
            sb.AppendLine();
            sb.AppendLine(LocalizationManager.Instance.Get("tech_level_on_reach", new object[] { nextNode.targetTechLevel }));
            sb.AppendLine($"{CommonUtility.Sprite("clockwork")} (Resource Cap)  {nextCap}h");
            sb.Append    ($"{CommonUtility.Sprite("spiky-field")} (Max Ships)  {nextMaxShips}");
        }

        UIManager.Instance.ShowAlertPopup(
            LocalizationManager.Instance.Get("tech_level_detail_title"),
            sb.ToString(),
            null
        );
    }

    private TechLevelResearchData GetNextTechLevelNode(Character character)
    {
        var techList = DataManager.Instance.m_dataTableResearch.TechLevelDataList;
        for (int i = 0; i < techList.Count; i++)
        {
            if (character.IsResearchCompleted(techList[i].researchId) == false)
                return techList[i];
        }
        return null;
    }
}
