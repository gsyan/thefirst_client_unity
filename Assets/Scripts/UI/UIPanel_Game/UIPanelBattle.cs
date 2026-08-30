// 실전투 화면 패널 — 존 전투 중(IsBattleState) 표시. 풀스크린 투명 이미지로 3D 함선 터치를 막아
// 전투 중 함대관리 UI(UIPanelFleet)가 열리는 것을 방지하고, UIBattleView(카메라 포커스/전술 토글 버튼)와
// UIRewardCardBuffDisplay(보상카드 지속버프 표시)를 자식으로 둔다
using UnityEngine;

public class UIPanelBattle : UIPanelBase
{
    [SerializeField] private UIRewardCardBuffDisplay m_rewardCardBuffDisplay;
    [SerializeField] private UIBattleView m_battleView;

    private EUnitState m_fleetState = EUnitState.Idle;
    private bool m_isExplorationOpen = false;

    void Awake()
    {
        EventManager.Subscribe_MyFleetStateChanged(OnFleetStateChanged);
        EventManager.Subscribe_ExplorationTabOpened(OnExplorationTabOpened);
        EventManager.Subscribe_ExplorationTabClosed(OnExplorationTabClosed);
    }

    void OnDestroy()
    {
        EventManager.Unsubscribe_MyFleetStateChanged(OnFleetStateChanged);
        EventManager.Unsubscribe_ExplorationTabOpened(OnExplorationTabOpened);
        EventManager.Unsubscribe_ExplorationTabClosed(OnExplorationTabClosed);
    }

    private void OnFleetStateChanged(EUnitState state)
    {
        m_fleetState = state;
        RefreshVisibility();
    }

    private void OnExplorationTabOpened()
    {
        m_isExplorationOpen = true;
        RefreshVisibility();
    }

    private void OnExplorationTabClosed()
    {
        m_isExplorationOpen = false;
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        // Tutorial_FirstPlay_Battle 연출 중에는 이 패널(존 정보/전술 토글 등)을 노출하지 않음
        bool isTutorialBattle = TutorialActionGate.IsTutorial("Tutorial_FirstPlay_Battle");

        if (m_fleetState.IsBattleState() == true && m_isExplorationOpen == false && isTutorialBattle == false)
            UIManager.Instance.ShowPanel(panelName);
        else
            UIManager.Instance.HidePanel(panelName);
    }

    public override void OnShowUIPanel()
    {
        if (m_battleView != null)
            m_battleView.RefreshTacticsDisplay();

        // 카드 선택은 그리드(전투 밖)에서만 일어나고 전투 중엔 바뀌지 않으므로, 패널이 뜰 때 1회 동기화면 충분
        if (m_rewardCardBuffDisplay != null)
            m_rewardCardBuffDisplay.Refresh(ObjectManager.Instance.m_rewardCardSessionState);
    }
}
