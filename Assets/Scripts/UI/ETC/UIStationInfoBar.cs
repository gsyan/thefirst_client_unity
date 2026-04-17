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
        int maxShips     = DataManager.Instance.m_dataTableResearch.GetShipCount(currentLevel);
        TechLevelResearchData nextNode = GetNextTechLevelNode(character);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{CommonUtility.Sprite("gears")} {currentLevel}  {CommonUtility.Sprite("mine-wagon")} {FormatHours(DataManager.Instance.m_dataTableResearch.GetStackTime(currentLevel))}  {CommonUtility.Sprite("spaceship")} {maxShips}");

        if (nextNode != null)
        {
            int nextMaxShips = DataManager.Instance.m_dataTableResearch.GetShipCount(nextNode.targetTechLevel);
            sb.AppendLine();
            sb.AppendLine(LocalizationManager.Instance.Get("tech_level_on_reach", new object[] { nextNode.targetTechLevel }));
            sb.AppendLine($"{CommonUtility.Sprite("mine-wagon")} (Resource Cap)  {FormatHours(DataManager.Instance.m_dataTableResearch.GetStackTime(nextNode.targetTechLevel))}");
            sb.Append    ($"{CommonUtility.Sprite("spaceship")} (Max Ships)  {nextMaxShips}");
        }

        UIManager.Instance.ShowAlertPopup(
            LocalizationManager.Instance.Get("tech_level_detail_title"),
            sb.ToString(),
            null
        );
    }

    // 소수 시간을 "XH YM" 형식으로 변환 (예: 3.5 → "3H 30M")
    private string FormatHours(float hours)
    {
        int h = (int)hours;
        int m = Mathf.RoundToInt((hours - h) * 60f);
        return m > 0 ? $"{h}H {m}M" : $"{h}H";
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
