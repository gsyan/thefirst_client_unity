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

    private ScrollRect m_scrollRect;

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
        m_scrollRect = GetComponentInChildren<ScrollRect>(true);
        if (m_confirmButton != null) m_confirmButton.onClick.AddListener(OnConfirmClicked);
        if (m_closeButton != null) m_closeButton.onClick.AddListener(HidePopup);
    }

    // sourceModule: 현재 선택된 모듈 / onConfirm: 교체 대상 subType 전달 콜백
    public void ShowPopup(ModuleBase sourceModule, Action<EModuleSubType> onConfirm)
    {
        base.ShowPopup();

        m_sourceModule = sourceModule;
        m_onConfirm = onConfirm;
        m_selectedSubType = EModuleSubType.none;

        if (m_titleText != null)
        {
            string typeKey = $"module_type_{sourceModule.GetModuleType().ToLocKey()}";
            m_titleText.text = LocalizationManager.Instance.Get("ship_module_subtype_manage", new object[] { LocalizationManager.Instance.Get(typeKey) });
        }

        BuildTree();
        TryAutoSelectNextStep();
        RefreshNodeColors();
        UpdateInfoPanel();
        ScrollToSelectedOrCurrent();
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
        // 배치된 노드들의 실제 바운딩박스 (노드 크기 포함)
        float boundsMinX = float.MaxValue, boundsMaxX = float.MinValue;
        float boundsMinY = float.MaxValue, boundsMaxY = float.MinValue;

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
                // 서브타입 이름은 코드에서 동적 생성 (CSV 키 불필요)
                string displayName = subType.GetLocalizedName();
                item.InitializeScrollViewResearchItem(displayName, () => OnNodeClicked(nodeId, subType), false);
                m_spawnedItems[node.researchId] = item;
            }

            m_spawnedNodes[node.researchId] = rect;

            Vector2 halfSize = rect.sizeDelta * 0.5f;
            float nodeMinX = node.uiPosition.x - halfSize.x;
            float nodeMaxX = node.uiPosition.x + halfSize.x;
            float nodeMinY = node.uiPosition.y - halfSize.y;
            float nodeMaxY = node.uiPosition.y + halfSize.y;
            if (nodeMinX < boundsMinX) boundsMinX = nodeMinX;
            if (nodeMaxX > boundsMaxX) boundsMaxX = nodeMaxX;
            if (nodeMinY < boundsMinY) boundsMinY = nodeMinY;
            if (nodeMaxY > boundsMaxY) boundsMaxY = nodeMaxY;
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
        RectTransform contentRect = m_contentContainer as RectTransform;
        if (contentRect == null && m_contentContainer != null)
            contentRect = m_contentContainer.GetComponent<RectTransform>();
        if (contentRect != null && boundsMaxX > boundsMinX)
            contentRect.sizeDelta = new Vector2(boundsMaxX - boundsMinX + 10, boundsMaxY - boundsMinY + 10);

        RefreshNodeColors();
    }

    // 현재 서브타입의 직접 다음 단계가 있으면 자동 선택
    private void TryAutoSelectNextStep()
    {
        EModuleSubType currentSubType = m_sourceModule.GetModuleSubType();
        ModuleResearchData currentNode = DataManager.Instance.m_dataTableResearch.GetResearchData(currentSubType);
        string currentResearchId = currentNode?.researchId ?? "";
        if (string.IsNullOrEmpty(currentResearchId) == true) return;

        for (int i = 0; i < m_currentNodeList.Count; i++)
        {
            var node = m_currentNodeList[i];
            if (node.prerequisiteIds != null && node.prerequisiteIds.Contains(currentResearchId))
            {
                m_selectedSubType = node.moduleSubType;
                break;
            }
        }
    }

    // 선택된 노드(없으면 현재 장착 노드)를 스크롤 중앙에 배치
    private void ScrollToSelectedOrCurrent()
    {
        if (m_scrollRect == null) return;

        EModuleSubType targetSubType = m_selectedSubType != EModuleSubType.none
            ? m_selectedSubType
            : m_sourceModule.GetModuleSubType();
        RectTransform targetRect = null;

        for (int i = 0; i < m_currentNodeList.Count; i++)
        {
            if (m_currentNodeList[i].moduleSubType == targetSubType)
            {
                m_spawnedNodes.TryGetValue(m_currentNodeList[i].researchId, out targetRect);
                break;
            }
        }

        if (targetRect == null) return;

        Canvas.ForceUpdateCanvases();

        RectTransform content = m_scrollRect.content;
        RectTransform viewport = m_scrollRect.viewport != null
            ? m_scrollRect.viewport
            : (RectTransform)m_scrollRect.transform;

        Vector2 childLocal = (Vector2)content.InverseTransformPoint(targetRect.position);

        if (m_scrollRect.horizontal == true)
        {
            float scrollable = content.rect.width - viewport.rect.width;
            if (scrollable > 0f)
            {
                float offset = childLocal.x - content.rect.xMin - viewport.rect.width * 0.5f;
                m_scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(offset / scrollable);
            }
        }

        if (m_scrollRect.vertical == true)
        {
            float scrollable = content.rect.height - viewport.rect.height;
            if (scrollable > 0f)
            {
                float offset = content.rect.yMax - childLocal.y - viewport.rect.height * 0.5f;
                m_scrollRect.verticalNormalizedPosition = 1f - Mathf.Clamp01(offset / scrollable);
            }
        }
    }

    private void OnNodeClicked(string nodeId, EModuleSubType subType)
    {
        m_selectedSubType = subType;
        RefreshNodeColors();
        UpdateInfoPanel();
    }

    // 노드 배경색 및 선택 아웃라인 갱신
    private void RefreshNodeColors()
    {
        EModuleSubType currentSubType = m_sourceModule.GetModuleSubType();
        int playerTechLevel = DataManager.Instance.m_currentCharacter?.GetTechLevel() ?? 0;
        ModuleResearchData currentNode = DataManager.Instance.m_dataTableResearch.GetResearchData(currentSubType);
        string currentResearchId = currentNode?.researchId ?? "";

        for (int i = 0; i < m_currentNodeList.Count; i++)
        {
            var node = m_currentNodeList[i];
            if (m_spawnedItems.TryGetValue(node.researchId, out ScrollViewResearchItem item) == false) continue;

            bool isSelected = node.moduleSubType == m_selectedSubType;
            EResearchNodeState state;

            bool isDirectNextStep = string.IsNullOrEmpty(currentResearchId) == false
                && node.prerequisiteIds != null
                && node.prerequisiteIds.Contains(currentResearchId);

            if (node.moduleSubType.GetTechTier() > playerTechLevel)
                state = EResearchNodeState.Locked;       // 어둠 = 기술레벨 부족
            else if (node.moduleSubType == currentSubType)
                state = EResearchNodeState.Current;      // 파랑 = 현재 장착
            else if (m_sourceModule.IsSubTypeFree(node.moduleSubType))
                state = EResearchNodeState.Researched;   // 초록 = 비용 없음 (이미 해금)
            else if (isDirectNextStep == true)
                state = EResearchNodeState.Researchable; // 회색 = MR 비용 (직접 다음 단계)
            else
                state = EResearchNodeState.Locked;       // 어둠 = 직접 다음 단계 아님

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
            // 신규 unlock → 직접 다음 단계 + max level 조건 + 기술레벨 + 비용 동시 체크
            ModuleResearchData currentNode = DataManager.Instance.m_dataTableResearch.GetResearchData(currentSubType);
            ModuleResearchData selectedNode = DataManager.Instance.m_dataTableResearch.GetResearchData(m_selectedSubType);
            bool isDirectNextStep = selectedNode != null && currentNode != null
                && selectedNode.prerequisiteIds != null
                && selectedNode.prerequisiteIds.Contains(currentNode.researchId);
            if (isDirectNextStep == false) canConfirm = false;

            // 선택된 서브타입의 prerequisite 서브타입 (레벨 조건 표시 기준)
            EModuleSubType prereqSubType = currentSubType;
            if (selectedNode?.prerequisiteIds != null && selectedNode.prerequisiteIds.Count > 0)
            {
                string prereqId = selectedNode.prerequisiteIds[0];
                var prereqNode = DataManager.Instance.m_dataTableResearch.ResearchDataList.Find(r => r.researchId == prereqId);
                if (prereqNode != null) prereqSubType = prereqNode.moduleSubType;
            }

            int currentLevel = m_sourceModule.GetModuleLevel();
            int maxLevel = DataManager.Instance.GetMaxModuleLevel(prereqSubType);
            // 현재 서브타입이 prerequisite과 일치해야 하고, 그 레벨이 맥스여야 함
            bool isMaxLevel = currentSubType == prereqSubType && currentLevel >= maxLevel;
            if (isMaxLevel == false) canConfirm = false;

            long mineralCost = DataManager.Instance.m_dataTableResearch.GetResearchCost(m_selectedSubType);
            var info = DataManager.Instance.m_currentCharacter?.m_characterInfo;
            bool insM  = (info?.mineral ?? 0) < mineralCost;
            bool insufficient = insM;
            if (insufficient == true) canConfirm = false;

            // prerequisite 서브타입 이름 기준으로 레벨 조건 표시
            string subtypeName = prereqSubType.GetLocalizedName();
            string levelMsg = LocalizationManager.Instance.Get(
                "module_subtype_max_level_required",
                new object[] { subtypeName, maxLevel, currentLevel });
            sb.Append(isMaxLevel == true
                ? levelMsg
                : $"<color=red>{levelMsg}</color>");
            sb.Append("\n");

            // 기술 레벨 조건 (부족하면 빨간색)
            string techLine = hasTechLevel
                ? $"{CommonUtility.Sprite("gears")} Lv.{requiredTechTier}"
                : $"{CommonUtility.Sprite("gears")} <color=red>Lv.{requiredTechTier}</color>";
            sb.Append(techLine);
            sb.Append("\n");

            // 4종 재화 비용 — 한 줄로 이어서 표시, 0이면 생략
            var costSb = new System.Text.StringBuilder();
            if (mineralCost > 0)
                costSb.Append(insM ? $"{CommonUtility.Sprite("crystal-growth")} <color=red>{CommonUtility.FormatBigNumber(mineralCost)}</color>" : $"{CommonUtility.Sprite("crystal-growth")} {CommonUtility.FormatBigNumber(mineralCost)}");
            if (costSb.Length > 0) sb.Append(costSb);
        }

        if (m_costText != null)
        {
            m_costText.text = sb.ToString().TrimEnd();
            // 텍스트 변경 후 부모 레이아웃 갱신
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_costText.transform.parent as RectTransform);
        }

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
