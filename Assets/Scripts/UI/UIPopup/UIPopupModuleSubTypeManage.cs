// 모듈 서브타입 관리 팝업 (서브타입 교체 선택 UI)
// 연구 트리 스타일로 교체 가능한 서브타입을 표시 — Current(파랑), Unlocked(초록), Available(회색)
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPopupModuleSubTypeManage : UIPopupBase
{
    [Header("Tree View")]
    [SerializeField] private Transform m_contentContainer;
    [SerializeField] private GameObject m_nodePrefab;
    [SerializeField] private GameObject m_linePrefab;

    [Header("Info Panel")]
    [SerializeField] private TMP_Text m_titleText;
    [SerializeField] private TMP_Text m_costText;        // "FREE" 또는 "5,000 MR"
    [SerializeField] private Button m_confirmButton;
    [SerializeField] private Button m_closeButton;

    // 현재 선택된 모듈 (교체 원본)
    private ModuleBase m_sourceModule;
    // 팝업에서 선택된 교체 대상 서브타입
    private EModuleSubType m_selectedSubType = EModuleSubType.none;

    private Action<EModuleSubType> m_onConfirm;

    // 노드/라인 풀
    private readonly List<RectTransform> m_nodePool = new List<RectTransform>();
    private readonly List<RectTransform> m_linePool = new List<RectTransform>();
    private int m_nodeActiveCount = 0;
    private int m_lineActiveCount = 0;

    private readonly Dictionary<string, RectTransform> m_spawnedNodes = new Dictionary<string, RectTransform>();
    private readonly Dictionary<string, ScrollViewResearchItem> m_spawnedItems = new Dictionary<string, ScrollViewResearchItem>();
    private readonly List<ModuleResearchData> m_currentNodeList = new List<ModuleResearchData>();

    protected override void Awake()
    {
        base.Awake();
        if (m_confirmButton != null) m_confirmButton.onClick.AddListener(OnConfirmClicked);
        if (m_closeButton != null) m_closeButton.onClick.AddListener(HidePopup);
    }

    // sourceModule: 현재 선택된 모듈 / onConfirm: 교체 대상 subType 전달 콜백
    public void ShowPopup(ModuleBase sourceModule, Action<EModuleSubType> onConfirm)
    {
        m_sourceModule = sourceModule;
        m_onConfirm = onConfirm;
        m_selectedSubType = EModuleSubType.none;

        if (m_titleText != null)
        {
            string typeKey = $"module_type_{sourceModule.GetModuleType().ToLocKey()}";
            m_titleText.text = LocalizationManager.Instance.Get("ship_module_subtype_manage", new object[] { LocalizationManager.Instance.Get(typeKey) });
        }

        BuildTree();
        UpdateInfoPanel();
        base.ShowPopup();
    }

    private void BuildTree()
    {
        // 활성 노드/라인 반환
        for (int i = 0; i < m_nodeActiveCount; i++)
            m_nodePool[i].gameObject.SetActive(false);
        for (int i = 0; i < m_lineActiveCount; i++)
            m_linePool[i].gameObject.SetActive(false);
        m_nodeActiveCount = 0;
        m_lineActiveCount = 0;
        m_spawnedNodes.Clear();
        m_spawnedItems.Clear();
        m_currentNodeList.Clear();

        EModuleType moduleType = m_sourceModule.GetModuleType();
        var allNodes = DataManager.Instance.m_dataTableResearch.GetResearchDataByType(moduleType);
        float maxAbsX = 0f, maxAbsY = 0f;

        // 해당 모듈 타입의 전체 서브타입 노드 표시 (연구 여부와 무관)
        for (int i = 0; i < allNodes.Count; i++)
        {
            var node = allNodes[i];

            m_currentNodeList.Add(node);

            RectTransform rect = GetNodeFromPool(m_nodeActiveCount);
            m_nodeActiveCount++;
            rect.gameObject.SetActive(true);
            rect.gameObject.name = $"Node_{node.researchId}";
            rect.anchoredPosition = node.uiPosition;

            ScrollViewResearchItem item = rect.GetComponentInChildren<ScrollViewResearchItem>();
            if (item != null)
            {
                string nodeId = node.researchId;
                EModuleSubType subType = node.moduleSubType;
                item.InitializeScrollViewResearchItem(node.researchId, () => OnNodeClicked(nodeId, subType));
                m_spawnedItems[node.researchId] = item;
            }

            m_spawnedNodes[node.researchId] = rect;
            if (Mathf.Abs(node.uiPosition.x) > maxAbsX) maxAbsX = Mathf.Abs(node.uiPosition.x);
            if (Mathf.Abs(node.uiPosition.y) > maxAbsY) maxAbsY = Mathf.Abs(node.uiPosition.y);
        }

        // 연결선 생성
        for (int i = 0; i < m_currentNodeList.Count; i++)
        {
            var node = m_currentNodeList[i];
            if (node.prerequisiteIds == null || node.prerequisiteIds.Count == 0) continue;
            if (m_spawnedNodes.TryGetValue(node.researchId, out RectTransform curr) == false) continue;
            for (int j = 0; j < node.prerequisiteIds.Count; j++)
            {
                if (m_spawnedNodes.TryGetValue(node.prerequisiteIds[j], out RectTransform prereq))
                    DrawConnection(prereq, curr);
            }
        }

        // Content 크기 갱신
        RectTransform contentRect = m_contentContainer?.GetComponent<RectTransform>();
        if (contentRect != null)
            contentRect.sizeDelta = new Vector2(maxAbsX * 2f, maxAbsY * 2f);

        RefreshNodeColors();
    }

    private void OnNodeClicked(string nodeId, EModuleSubType subType)
    {
        m_selectedSubType = subType;
        RefreshNodeColors();
        UpdateInfoPanel();
    }

    private void RefreshNodeColors()
    {
        EModuleSubType currentSubType = m_sourceModule.GetModuleSubType();
        int playerTechLevel = DataManager.Instance.m_currentCharacter?.GetTechLevel() ?? 0;

        for (int i = 0; i < m_currentNodeList.Count; i++)
        {
            var node = m_currentNodeList[i];
            if (m_spawnedItems.TryGetValue(node.researchId, out ScrollViewResearchItem item) == false) continue;

            bool isSelected = node.moduleSubType == m_selectedSubType;
            EResearchNodeState state;

            if (node.moduleSubType.GetTechTier() > playerTechLevel)
                state = EResearchNodeState.Locked;       // 어둠 = 기술레벨 부족
            else if (node.moduleSubType == currentSubType)
                state = EResearchNodeState.Current;      // 파랑 = 현재 장착
            else if (m_sourceModule.IsSubTypeFree(node.moduleSubType))
                state = EResearchNodeState.Researched;   // 초록 = 비용 없음
            else
                state = EResearchNodeState.Researchable; // 회색 = MR 비용

            item.SetNodeState(state, isSelected);
        }
    }

    private void UpdateInfoPanel()
    {
        EModuleSubType currentSubType = m_sourceModule.GetModuleSubType();
        bool nothingSelected = m_selectedSubType == EModuleSubType.none || m_selectedSubType == currentSubType;
        if (nothingSelected == true)
        {
            if (m_costText != null) m_costText.text = "";
            if (m_confirmButton != null) m_confirmButton.interactable = false;
            return;
        }

        int playerTechLevel = DataManager.Instance.m_currentCharacter?.GetTechLevel() ?? 0;
        int requiredTechTier = m_selectedSubType.GetTechTier();
        bool hasTechLevel = playerTechLevel >= requiredTechTier;

        bool isFree = m_sourceModule.IsSubTypeFree(m_selectedSubType);
        bool canConfirm = hasTechLevel;

        var sb = new System.Text.StringBuilder();

        if (isFree == true)
        {
            // 이미 unlock된 서브타입 → 자유 교체, 비용 조건 없음
            sb.Append(LocalizationManager.Instance.Get("free"));
        }
        else
        {
            // 신규 unlock → max level 조건 + 기술레벨 + 비용 동시 체크
            int currentLevel = m_sourceModule.GetModuleLevel();
            int maxLevel = DataManager.Instance.GetMaxModuleLevel(currentSubType);
            bool isMaxLevel = currentLevel >= maxLevel;
            if (isMaxLevel == false) canConfirm = false;

            CostStruct cost = DataManager.Instance.m_dataTableResearch.GetResearchCost(m_selectedSubType);
            long have = DataManager.Instance.m_currentCharacter?.m_characterInfo?.mineralRare ?? 0;
            bool insufficient = have < cost.mineralRare;
            if (insufficient == true) canConfirm = false;

            // 현재 장착 서브타입 이름 조회 (researchId = loc key)
            string subtypeName = currentSubType.ToString();
            for (int i = 0; i < m_currentNodeList.Count; i++)
            {
                if (m_currentNodeList[i].moduleSubType == currentSubType)
                {
                    subtypeName = LocalizationManager.Instance.Get(m_currentNodeList[i].researchId);
                    break;
                }
            }
            string levelMsg = LocalizationManager.Instance.Get(
                "module_subtype_max_level_required",
                new object[] { subtypeName, maxLevel, currentLevel });
            sb.Append(isMaxLevel == true
                ? levelMsg
                : $"<color=red>{levelMsg}</color>");
            sb.Append("\n\n");

            // 기술 레벨 조건 (부족하면 빨간색)
            string techLine = hasTechLevel
                ? $"<sprite name=\"IconTech\"> Lv.{requiredTechTier}"
                : $"<sprite name=\"IconTech\"> <color=red>Lv.{requiredTechTier}</color>";
            sb.Append(techLine);
            sb.Append("\n\n");

            string costLine = insufficient
                ? $"<sprite name=\"IconMineralR\"> <color=red>{CommonUtility.FormatBigNumber(cost.mineralRare)}</color>"
                : $"<sprite name=\"IconMineralR\"> {CommonUtility.FormatBigNumber(cost.mineralRare)}";
            sb.Append(costLine);
        }

        if (m_costText != null) m_costText.text = sb.ToString().TrimEnd();

        if (m_confirmButton != null) m_confirmButton.interactable = canConfirm;
    }

    private void OnConfirmClicked()
    {
        if (m_selectedSubType == EModuleSubType.none) return;
        m_onConfirm?.Invoke(m_selectedSubType);
        HidePopup();
    }

    private RectTransform GetNodeFromPool(int index)
    {
        if (index < m_nodePool.Count) return m_nodePool[index];
        GameObject obj = Instantiate(m_nodePrefab, m_contentContainer);
        RectTransform rect = obj.GetComponent<RectTransform>();
        m_nodePool.Add(rect);
        return rect;
    }

    private void DrawConnection(RectTransform start, RectTransform end)
    {
        if (m_linePrefab == null) return;
        RectTransform lineRect = GetLineFromPool(m_lineActiveCount);
        m_lineActiveCount++;
        lineRect.gameObject.SetActive(true);
        lineRect.transform.SetAsFirstSibling();

        Vector2 dir = end.anchoredPosition - start.anchoredPosition;
        float dist = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        lineRect.anchoredPosition = start.anchoredPosition + dir * 0.5f;
        lineRect.sizeDelta = new Vector2(dist, 5f);
        lineRect.localRotation = Quaternion.Euler(0, 0, angle);
    }

    private RectTransform GetLineFromPool(int index)
    {
        if (index < m_linePool.Count) return m_linePool[index];
        GameObject obj = Instantiate(m_linePrefab, m_contentContainer);
        RectTransform rect = obj.GetComponent<RectTransform>();
        m_linePool.Add(rect);
        return rect;
    }
}
