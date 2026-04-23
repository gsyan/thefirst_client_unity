// 함대 탭 UI — Tech Level 행, Fleet Stats(2행 압축), 함선 선택 그리드(9칸 고정, 프리팹에 미리 배치), Formation 하단 바 + 교체 팝업 관리
// 빈 슬롯은 잠금 아이콘으로 표시, 클릭 시 함선 추가 팝업 호출
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UITabStation : UITabBase
{
    [Header("Tech Level 행")]
    [SerializeField] private TMP_Text m_techLevelText;
    [SerializeField] private Button   m_techLevelUpButton;
    [SerializeField] private TMP_Text m_techLevelInfoText;

    private Character m_myCharacter;
    private SpaceFleet m_myFleet;
    
    public override void InitializeUITab()
    {
        InitializeUITabStation();
    }

    private void InitializeUITabStation()
    {
        m_myCharacter = DataManager.Instance.m_currentCharacter;
        if (m_myCharacter == null || m_myCharacter.GetOwnedFleet() == null) return;
        m_myFleet = m_myCharacter.GetOwnedFleet();
        if (m_myFleet == null) return;

        if (m_techLevelUpButton != null) m_techLevelUpButton.onClick.AddListener(OnTechLevelButtonClicked);
        
        EventManager.Subscribe_TechLevelChanged(OnTechLevelChanged);
    }

    public override void OnTabActivated()
    {
        base.OnTabActivated();
        UpdateTechLevelDisplay();
    }

    public override void OnTabDeactivated()
    {
        base.OnTabDeactivated();
    }

    // ── Tech Level ────────────────────────────────────────────────────

    private void UpdateTechLevelDisplay()
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;

        int currentLevel = character.GetTechLevel();
        int maxShips = DataManager.Instance.m_dataTableResearch.GetShipCount(currentLevel);
        TechLevelResearchData nextNode = GetNextTechLevelNode(character);

        // 기술레벨 요약: 레벨 / 자원 보관 캡 / 최대 함선 수
        if (m_techLevelText != null)
            m_techLevelText.text = $"Lv.{currentLevel}";

        if (m_techLevelInfoText != null)
            m_techLevelInfoText.text = $"{CommonUtility.Sprite("spaceship")} {maxShips}";

        if (m_techLevelUpButton != null)
            m_techLevelUpButton.gameObject.SetActive(nextNode != null);
        
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_techLevelText.transform.parent as RectTransform);
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

    private void OnTechLevelButtonClicked()
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;

        if (GetNextTechLevelNode(character) == null) return;

        int currentLevel = character.GetTechLevel();
        UIManager.Instance.ShowTechLevelupPopup(currentLevel, targetLevel =>
        {
            ResearchTechLevelsSequentially(currentLevel + 1, targetLevel);
        });
    }

    // currentLevel+1 ~ toLevel 까지 순차적으로 API 호출
    private void ResearchTechLevelsSequentially(int fromLevel, int toLevel)
    {
        var techList = DataManager.Instance.m_dataTableResearch.TechLevelDataList;
        var node = techList.Find(n => n.targetTechLevel == fromLevel);
        if (node == null) return;

        var character = DataManager.Instance.m_currentCharacter;
        if (character.CheckEnoughMineral(node.mineralCost) == false)
        {
            ShowErrorMessage(LocalizationManager.Instance.Get("error_insufficient_resources"));
            return;
        }

        var request = new TechLevelResearchRequest { researchId = node.researchId };
        NetworkManager.Instance.ResearchTechLevel(request, response =>
        {
            OnSequentialTechLevelResponse(response, fromLevel, toLevel);
        });
    }

    private void OnSequentialTechLevelResponse(ApiResponse<TechLevelResearchResponse> response, int completedLevel, int toLevel)
    {
        if (response.errorCode != 0)
        {
            string errorMessage = ErrorCodeMapping.GetMessage(response.errorCode);
            ShowErrorMessage($"Research failed: {errorMessage}");
            return;
        }

        if (response.data.costRemainInfo != null)
            DataManager.Instance.m_currentCharacter.UpdateAllMinerals(response.data.costRemainInfo);
        if (response.data.researchedIds != null)
            DataManager.Instance.m_currentCharacter.SetCompletedResearchIds(response.data.researchedIds);

        UpdateTechLevelDisplay();

        int nextLevel = completedLevel + 1;
        if (nextLevel <= toLevel)
            ResearchTechLevelsSequentially(nextLevel, toLevel);
    }

    private void OnTechLevelChanged(int techLevel)
    {
        UpdateTechLevelDisplay();
    }

}
