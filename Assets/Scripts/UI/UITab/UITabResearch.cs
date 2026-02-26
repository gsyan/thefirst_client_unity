using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Mathematics;

public class UITabResearch : UITabBase
{
    [SerializeField] private Button m_techButton;
    [SerializeField] private Button m_bodyButton;
    [SerializeField] private Button m_engineButton;
    [SerializeField] private Button m_beamButton;
    [SerializeField] private Button m_missileButton;
    [SerializeField] private Button m_hangerButton;

    [Header("Research UI Components")]
    [SerializeField] private Transform m_contentContainer; // ScrollRect의 Content
    [SerializeField] private GameObject m_nodePrefab;      // 연구 아이콘 프리팹
    [SerializeField] private GameObject m_linePrefab;      // 노드 사이를 잇는 선 프리팹 (Image)

    [SerializeField] private TMP_Text  m_textModuleStatus;
    [SerializeField] private RectTransform m_moduleStatsContainer;

    [SerializeField] private Button m_researchButton;
    [SerializeField] private TMP_Text m_researchButtonText;

    private bool bShow = false;

    private SpaceFleet m_myFleet;
    private Character m_myCharacter;
    private DataTableResearch m_researchTable;
    private EModuleType m_currentModuleType = EModuleType.beam;
    private string m_selectedNodeId = "";

    private readonly List<RowLabelValue> m_moduleStatRows = new();

    // 현재 표시중인 노드 리스트 (ResearchNodeData 베이스)
    private List<ResearchNodeData> m_currentNodeList = new List<ResearchNodeData>();
    private Dictionary<string, RectTransform> m_spawnedNodes = new Dictionary<string, RectTransform>();
    private Dictionary<string, ScrollViewResearchItem> m_spawnedResearchItems = new Dictionary<string, ScrollViewResearchItem>();

    // 로컬 풀 (노드, 라인)
    private readonly List<RectTransform> m_nodePool = new List<RectTransform>();
    private readonly List<RectTransform> m_linePool = new List<RectTransform>();
    private int m_nodeActiveCount = 0;
    private int m_lineActiveCount = 0;

    public override void InitializeUITab()
    {
        InitializeUITabResearch();
    }

    private void InitializeUITabResearch()
    {
        m_myCharacter = DataManager.Instance.m_currentCharacter;
        if (m_myCharacter == null || m_myCharacter.GetOwnedFleet() == null) return;
        m_myFleet = m_myCharacter.GetOwnedFleet();

        m_researchTable = DataManager.Instance.m_dataTableResearch;

        // m_moduleStatsContainer 내부의 자식들을 하이어라키 순서대로 캐싱
        if (m_moduleStatsContainer != null)
        {
            m_moduleStatRows.Clear();
            for (int i = 0; i < m_moduleStatsContainer.childCount; i++)
            {
                Transform child = m_moduleStatsContainer.GetChild(i);
                var row = child.GetComponent<RowLabelValue>();
                if (row != null)
                    m_moduleStatRows.Add(row);
            }
        }

        m_bodyButton.onClick.AddListener(() => SwitchModuleType(EModuleType.body));
        m_engineButton.onClick.AddListener(() => SwitchModuleType(EModuleType.engine));
        m_beamButton.onClick.AddListener(() => SwitchModuleType(EModuleType.beam));
        m_missileButton.onClick.AddListener(() => SwitchModuleType(EModuleType.missile));
        m_hangerButton.onClick.AddListener(() => SwitchModuleType(EModuleType.hanger));

        m_researchButton.onClick.AddListener(() => OnModuleResearchClicked());

        SwitchModuleType(m_currentModuleType);
    }

    // 탭 전환 시 해당 모듈 타입의 연구 트리로 갱신
    private void SwitchModuleType(EModuleType moduleType)
    {
        m_currentModuleType = moduleType;

        // ModuleResearchData → ResearchNodeData 리스트로 변환
        m_currentNodeList.Clear();
        var moduleDataList = m_researchTable.GetResearchDataByType(moduleType);
        for (int i = 0; i < moduleDataList.Count; i++)
            m_currentNodeList.Add(moduleDataList[i]);

        m_selectedNodeId = "";
        GenerateResearchTree();
        UpdateResearchUI();
    }

    private void GenerateResearchTree()
    {
        if (m_contentContainer == null || m_nodePrefab == null) return;

        // 1. 기존 활성 노드/라인 비활성화
        for (int i = 0; i < m_nodeActiveCount; i++)
            m_nodePool[i].gameObject.SetActive(false);
        for (int i = 0; i < m_lineActiveCount; i++)
            m_linePool[i].gameObject.SetActive(false);

        m_nodeActiveCount = 0;
        m_lineActiveCount = 0;
        m_spawnedNodes.Clear();
        m_spawnedResearchItems.Clear();

        float maxAbsX = 0f;
        float maxAbsY = 0f;

        // 2. 노드 생성 (풀에서 가져오거나 새로 생성)
        for (int i = 0; i < m_currentNodeList.Count; i++)
        {
            ResearchNodeData nodeData = m_currentNodeList[i];
            RectTransform rect = GetNodeFromPool(m_nodeActiveCount);
            m_nodeActiveCount++;

            rect.gameObject.SetActive(true);
            rect.gameObject.name = $"Node_{nodeData.researchId}";
            rect.anchoredPosition = nodeData.uiPosition;

            ScrollViewResearchItem researchItemComp = rect.GetComponentInChildren<ScrollViewResearchItem>();
            if (researchItemComp != null)
            {
                string nodeId = nodeData.researchId;
                researchItemComp.InitializeScrollViewResearchItem(nodeData.researchId, () => OnNodeSelectClicked(nodeId));
                m_spawnedResearchItems[nodeId] = researchItemComp;
            }

            m_spawnedNodes[nodeData.researchId] = rect;

            if (Mathf.Abs(nodeData.uiPosition.x) > maxAbsX) maxAbsX = Mathf.Abs(nodeData.uiPosition.x);
            if (Mathf.Abs(nodeData.uiPosition.y) > maxAbsY) maxAbsY = Mathf.Abs(nodeData.uiPosition.y);
        }

        // 3. 연결 선 그리기 (선행조건이 복수일 수 있으므로 모두 연결)
        for (int i = 0; i < m_currentNodeList.Count; i++)
        {
            var nodeData = m_currentNodeList[i];
            if (nodeData.prerequisiteIds == null || nodeData.prerequisiteIds.Count == 0) continue;
            if (m_spawnedNodes.TryGetValue(nodeData.researchId, out RectTransform current) == false) continue;

            for (int j = 0; j < nodeData.prerequisiteIds.Count; j++)
            {
                if (m_spawnedNodes.TryGetValue(nodeData.prerequisiteIds[j], out RectTransform prereqNode))
                {
                    DrawConnection(prereqNode, current);
                }
            }
        }

        // 4. Content 사이즈 갱신
        RectTransform contentRect = m_contentContainer.GetComponent<RectTransform>();
        if (contentRect != null)
        {
            float padding = 0f;
            contentRect.sizeDelta = new Vector2(maxAbsX * 2f + padding, maxAbsY * 2f + padding);
        }
    }

    // 풀에서 노드 가져오기, 부족하면 새로 생성
    private RectTransform GetNodeFromPool(int poolIndex)
    {
        if (poolIndex < m_nodePool.Count)
            return m_nodePool[poolIndex];

        GameObject nodeObj = Instantiate(m_nodePrefab, m_contentContainer);
        RectTransform rect = nodeObj.GetComponent<RectTransform>();
        m_nodePool.Add(rect);
        return rect;
    }

    // 풀에서 라인 가져오기, 부족하면 새로 생성
    private RectTransform GetLineFromPool(int poolIndex)
    {
        if (poolIndex < m_linePool.Count)
            return m_linePool[poolIndex];

        GameObject lineObj = Instantiate(m_linePrefab, m_contentContainer);
        RectTransform rect = lineObj.GetComponent<RectTransform>();
        m_linePool.Add(rect);
        return rect;
    }

    private void DrawConnection(RectTransform start, RectTransform end)
    {
        if (m_linePrefab == null) return;

        RectTransform lineRect = GetLineFromPool(m_lineActiveCount);
        m_lineActiveCount++;

        lineRect.gameObject.SetActive(true);
        lineRect.transform.SetAsFirstSibling();

        Vector2 dir = (end.anchoredPosition - start.anchoredPosition);
        float distance = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        lineRect.anchoredPosition = start.anchoredPosition + (dir * 0.5f);
        lineRect.sizeDelta = new Vector2(distance, 5f);
        lineRect.localRotation = Quaternion.Euler(0, 0, angle);
    }

    public override void OnTabActivated()
    {
        bShow = true;
        //CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
        UpdateResearchUI();
    }

    public override void OnTabDeactivated()
    {
        bShow = false;

        //CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
    }

    private void OnNodeSelectClicked(string nodeId)
    {
        m_selectedNodeId = nodeId;
        UpdateResearchUI();
    }

    // 연구 노드의 완료 여부 판별 (타입별 분기)
    private bool IsNodeResearched(ResearchNodeData node)
    {
        if (node is ModuleResearchData module)
            return m_myCharacter.IsModuleResearched(module.moduleType, module.moduleSubType);
        // if (node is TechResearchData tech)
        //     return m_myCharacter.GetTechLevel() >= tech.m_targetTechLevel;
        return false;
    }

    // 모든 노드의 색상을 상태에 따라 갱신
    private void RefreshNodeColors()
    {
        for (int i = 0; i < m_currentNodeList.Count; i++)
        {
            ResearchNodeData nodeData = m_currentNodeList[i];
            if (m_spawnedResearchItems.TryGetValue(nodeData.researchId, out ScrollViewResearchItem item) == false) continue;

            bool isSelected = nodeData.researchId == m_selectedNodeId;
            EResearchNodeState baseState = IsNodeResearched(nodeData) ? EResearchNodeState.Researched : EResearchNodeState.Researchable;
            item.SetNodeState(baseState, isSelected);
        }
    }

    private void UpdateResearchUI()
    {
        if (bShow != true) return;

        // 선택된 노드가 없으면, 연구되지 않은 것 중 가장 앞의 노드를 선택
        if (string.IsNullOrEmpty(m_selectedNodeId))
        {
            m_selectedNodeId = GetFirstUnresearchedNodeId();
        }

        RefreshNodeColors();

        // 선택된 노드 찾기
        ResearchNodeData selectedNode = m_currentNodeList.Find(n => n.researchId == m_selectedNodeId);
        if (selectedNode is ModuleResearchData moduleNode)
        {
            UpdateModuleStatsDisplay(moduleNode.moduleSubType);
        }

        // 버튼 업데이트
        bool bResearched = IsNodeResearched(selectedNode);
        if(bResearched == true)            
            CommonUtility.SetUILocText(m_researchButtonText, "research_already");
        else
            CommonUtility.SetUILocText(m_researchButtonText, "research_module");
    }

    // 연구되지 않은 노드 중 리스트 순서상 가장 앞의 것을 반환, 모두 완료면 마지막 노드
    private string GetFirstUnresearchedNodeId()
    {
        for (int i = 0; i < m_currentNodeList.Count; i++)
        {
            if (IsNodeResearched(m_currentNodeList[i]) == false)
                return m_currentNodeList[i].researchId;
        }
        if (m_currentNodeList.Count > 0)
            return m_currentNodeList[m_currentNodeList.Count - 1].researchId;
        return "";
    }

    private void UpdateModuleStatsDisplay(EModuleSubType targetSubType)
    {
        int maxLevel = DataManager.Instance.m_dataTableModule.GetMaxLevel(targetSubType);
        CommonUtility.GetModuleStatRows(m_currentModuleType, targetSubType, 1, maxLevel, out var labels, out var values);
        for (int i = 0; i < m_moduleStatRows.Count; i++)
        {
            if (i < labels.Count)
                m_moduleStatRows[i].SetRow(labels[i], values[i]);
            else
                m_moduleStatRows[i].SetRow("empty_text", "");
        }
    }

    private void OnModuleResearchClicked()
    {
        // 선택된 노드에서 모듈 정보 추출
        ResearchNodeData selectedNode = m_currentNodeList.Find(n => n.researchId == m_selectedNodeId);
        if (selectedNode is not ModuleResearchData moduleNode) return;

        EModuleType moduleType = moduleNode.moduleType;
        EModuleSubType moduleSubType = moduleNode.moduleSubType;

        // 이미 연구 완료된 경우
        if (IsNodeResearched(selectedNode))
        {
            ShowResultMessage($"Already Researched", 3f);
            return;
        } 

        CostStruct researchCost = DataManager.Instance.GetModuleResearchCost(moduleSubType);
        string localizedSubType = LocalizationManager.Instance.Get(moduleSubType.ToLocKey());
        List<string> leftLabels = new List<string>{ "ship_module_type" };
        List<string> leftValues = new List<string>{ localizedSubType };

        UIManager.Instance.ShowConfirmPopup(
            LocalizationManager.Instance.Get("research_module"),
            LocalizationManager.Instance.Get("popup_message_module_research", new object[] { localizedSubType }),
            leftLabels, leftValues,
            researchCost,
            onConfirm: () =>
            {
                //CostStruct researchCost = DataManager.Instance.GetModuleResearchCost(moduleSubType);
                bool result = DataManager.Instance.m_currentCharacter.CheckEnoughCostStruct(researchCost);
                if (result == false)
                {
                    ShowResultMessage($"Insufficient resources(cost mineral: {CommonUtility.FormatBigNumber(researchCost.mineral)})", 3f);
                    return;
                }


                var request = new ModuleResearchRequest
                {
                    moduleType = moduleType
                    , moduleSubType = moduleSubType
                };

                NetworkManager.Instance.ResearchModule(request, OnModuleResearchResponse);
            },
            onCancel: () =>
            {
                ShowResultMessage("Research cancelled", 2f);
            }
        );
    }

    private void OnModuleResearchResponse(ApiResponse<ModuleResearchResponse> response)
    {
        if (response.errorCode == 0)
        {
            // Research successful
            var researchResponse = response.data;

            // Update character's remaining resources
            if (researchResponse.costRemainInfo != null)
                DataManager.Instance.m_currentCharacter.UpdateAllMinerals(researchResponse.costRemainInfo);

            // Update researched modules list
            if (researchResponse.researchedModuleTypes != null)
                DataManager.Instance.m_currentCharacter.UpdateResearchedModules(researchResponse.researchedModuleTypes);

            ShowResultMessage($"Research completed: {researchResponse.moduleType}-{researchResponse.moduleSubType}", 3f);

            UpdateResearchUI();
        }
        else
        {
            // Research failed
            string errorMessage = ErrorCodeMapping.GetMessage(response.errorCode);
            Debug.LogError($"Research failed: {errorMessage}");
            ShowResultMessage($"Research failed: {errorMessage}", 3f);
        }
    }
}
