// 실전투 화면 패널 — 존 전투 중(IsBattleState) 표시. 풀스크린 투명 이미지로 3D 함선 터치를 막아
// 전투 중 함대관리 UI(UIPanelFleet)가 열리는 것을 방지하고, UIBattleView(카메라 포커스/전술 토글 버튼)와
// UIRewardCardBuffDisplay(보상카드 지속버프 표시)를 자식으로 둔다.
// 전술력(TacticPower) 소모 계산도 이 패널이 담당 — 패널이 꺼지면(전투 종료) 코루틴도 Unity가 자동으로 멈춰줌
using System.Collections;
using UnityEngine;

public class UIPanelBattle : UIPanelBase
{
    [SerializeField] private UIRewardCardBuffDisplay m_rewardCardBuffDisplay;
    [SerializeField] private UIBattleView m_battleView;

    private EUnitState m_fleetState = EUnitState.Idle;
    private bool m_isExplorationOpen = false;

    // 전술력은 전투 중 서버에 실시간 저장하지 않음 — 셀 클리어 성공 시 ClearExplorationCellRequest에 실어 확정 저장,
    // 실패(퇴각/패배)면 UIPanelExplorationGrid가 진입 직전 스냅샷으로 로컬 롤백(서버는 애초에 그 변화를 모름)
    private Coroutine m_tacticPowerDrainCoroutine;
    private static readonly WaitForSeconds k_oneSecondWait = new WaitForSeconds(1f);

    void Awake()
    {
        EventManager.Subscribe_MyFleetStateChanged(OnFleetStateChanged);
        EventManager.Subscribe_ExplorationTabOpened(OnExplorationTabOpened);
        EventManager.Subscribe_ExplorationTabClosed(OnExplorationTabClosed);
        EventManager.Subscribe_TacticToggleRequested(OnTacticToggleRequested);
    }

    void OnDestroy()
    {
        EventManager.Unsubscribe_MyFleetStateChanged(OnFleetStateChanged);
        EventManager.Unsubscribe_ExplorationTabOpened(OnExplorationTabOpened);
        EventManager.Unsubscribe_ExplorationTabClosed(OnExplorationTabClosed);
        EventManager.Unsubscribe_TacticToggleRequested(OnTacticToggleRequested);
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

        CommanderInfo commanderInfo = GetCommanderInfo();
        if (commanderInfo != null)
            EventManager.Trigger_TacticPowerChanged(commanderInfo.tacticPower, commanderInfo.tacticPowerMax);

        if (m_tacticPowerDrainCoroutine == null)
            m_tacticPowerDrainCoroutine = StartCoroutine(Co_DrainTacticPower());
    }

    public override void OnHideUIPanel()
    {
        if (m_tacticPowerDrainCoroutine != null)
        {
            StopCoroutine(m_tacticPowerDrainCoroutine);
            m_tacticPowerDrainCoroutine = null;
        }
    }

    private CommanderInfo GetCommanderInfo()
    {
        return DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
    }

    // 켜진 토글 개수만큼 1초에 한 번씩 소모 — DataTableConfig.gameSettings의 3개 초당 소모 필드를 그대로 합산
    private IEnumerator Co_DrainTacticPower()
    {
        while (true)
        {
            yield return k_oneSecondWait;

            CommanderInfo commanderInfo = GetCommanderInfo();
            SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
            if (commanderInfo == null || myFleet == null || myFleet.m_fleetInfo == null) continue;
            if (commanderInfo.tacticPower <= 0) continue;

            int tacticOptions = myFleet.m_fleetInfo.tacticOptions;
            GameSettings gameSettings = DataManager.Instance.m_dataTableConfig.gameSettings;

            // 토글이 켜져 있어도 실질 효과가 없는 상태(체력 만땅/미사일·함재기 모듈 없음/실드 게이지 없음)면 전술력을 소모하지 않음 — 토글 자체는 유지(조건이 다시 성립하면 재소모)
            bool repairHasEffect = myFleet.GetFleetHealthRatio() < 1f;
            CapabilityProfile fleetProfile = myFleet.GetFleetCapabilityProfile();
            bool missileHasEffect = fleetProfile.missileAttack > 0f;
            bool aircraftHasEffect = fleetProfile.airCount > 0;
            bool shieldHasEffect = myFleet.HasAnyShieldDefending();

            int drainPerSec = 0;
            if ((tacticOptions & (1 << 0)) != 0 && repairHasEffect == true) drainPerSec += gameSettings.repairBoostExplorationPointPerSec;
            if ((tacticOptions & (1 << 1)) != 0 && missileHasEffect == true) drainPerSec += gameSettings.missileTacticExplorationPointPerSec;
            if ((tacticOptions & (1 << 2)) != 0 && aircraftHasEffect == true) drainPerSec += gameSettings.aircraftTacticExplorationPointPerSec;
            if ((tacticOptions & (1 << 3)) != 0 && shieldHasEffect == true) drainPerSec += gameSettings.shieldTacticExplorationPointPerSec;
            if (drainPerSec <= 0) continue;

            commanderInfo.tacticPower = Mathf.Max(0, commanderInfo.tacticPower - drainPerSec);
            EventManager.Trigger_TacticPowerChanged(commanderInfo.tacticPower, commanderInfo.tacticPowerMax);

            if (commanderInfo.tacticPower <= 0)
                TurnOffAllTacticToggles(myFleet);
        }
    }

    // 전투 중 함선 터치가 막혀 토글 버튼은 UIBattleView 클릭으로만 켜짐 — idx: 0=수리, 1=미사일, 2=함재기, 3=실드(EventManager.OnTacticOptionsChanged 주석과 동일)
    private void OnTacticToggleRequested(int idx)
    {
        SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
        CommanderInfo commanderInfo = GetCommanderInfo();
        if (myFleet == null || myFleet.m_fleetInfo == null || commanderInfo == null) return;

        int bit = 1 << idx;
        bool turningOn = (myFleet.m_fleetInfo.tacticOptions & bit) == 0;
        if (turningOn == true && commanderInfo.tacticPower <= 0) return; // 전술력이 없으면 새로 켤 수 없음

        int newOptions = turningOn ? (myFleet.m_fleetInfo.tacticOptions | bit) : (myFleet.m_fleetInfo.tacticOptions & ~bit);
        ApplyTacticOptions(myFleet, newOptions);
    }

    private void TurnOffAllTacticToggles(SpaceFleet myFleet)
    {
        if (myFleet.m_fleetInfo.tacticOptions == 0) return;
        ApplyTacticOptions(myFleet, 0);
    }

    private void ApplyTacticOptions(SpaceFleet myFleet, int newOptions)
    {
        myFleet.m_fleetInfo.tacticOptions = newOptions;
        EventManager.Trigger_TacticOptionsChanged(newOptions);

        long fleetId = myFleet.m_fleetInfo.id;

        NetworkManager.Instance.ChangeTacticOptions(new ChangeTacticOptionsRequest
        {
            fleetId = fleetId,
            tacticOptions = newOptions,
        }, response =>
        {
            if (response.errorCode != 0)
                Debug.LogError($"[UIPanelBattle] ChangeTacticOptions 실패: {response.errorCode} fleetId={fleetId}");
        });
    }
}
