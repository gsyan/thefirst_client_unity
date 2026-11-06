// 실전투 화면 패널 — 존 전투 중(IsBattleState) 표시. 풀스크린 투명 이미지로 3D 함선 터치를 막아
// 전투 중 함대관리 UI(UIPanelFleet)가 열리는 것을 방지하고, UIBattleView(카메라 포커스/전술 토글 버튼)와
// UIRewardCardBuffDisplay(보상카드 지속버프 표시)를 자식으로 둔다.
// 자원(Resource) 소모 계산도 이 패널이 담당 — 패널이 꺼지면(전투 종료) 코루틴도 Unity가 자동으로 멈춰줌
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class UIPanelBattle : UIPanelBase
{
    [SerializeField] private UIRewardCardBuffDisplay m_rewardCardBuffDisplay;
    [SerializeField] private UIBattleView m_battleView;

    [Header("자원 게이지")]
    [SerializeField] private TMP_Text m_gaugeNameText; // 게이지 위 라벨("Resources")
    [SerializeField] private Image m_tacticPowerGauge; // 자원 게이지(Filled/Horizontal)
    [FormerlySerializedAs("m_mineralAmountText")]
    [SerializeField] private TMP_Text m_resourceText; // 게이지바 위에 겹쳐 표시되는 현재치/최대치 텍스트("968 / 1,000")

    private EUnitState m_fleetState = EUnitState.Idle;
    private bool m_isExplorationOpen = false;

    // 자원은 전투 중 서버에 실시간 저장하지 않음 — 셀 클리어 성공 시 ClearExplorationCellRequest에 실어 확정 저장,
    // 실패(퇴각/패배)면 UIPanelExplorationGrid가 진입 직전 스냅샷으로 로컬 롤백(서버는 애초에 그 변화를 모름)
    private Coroutine m_resourceDrainCoroutine;
    private const float k_tacticTickSeconds = 1f;
    private static readonly WaitForSeconds k_oneSecondWait = new WaitForSeconds(k_tacticTickSeconds);

    void Awake()
    {
        EventManager.Subscribe_MyFleetStateChanged(OnFleetStateChanged);
        EventManager.Subscribe_ExplorationTabOpened(OnExplorationTabOpened);
        EventManager.Subscribe_ExplorationTabClosed(OnExplorationTabClosed);
        EventManager.Subscribe_ZoneRunEnded(OnZoneRunEnded);
        EventManager.Subscribe_ResourceChanged(OnResourceChanged);
        EventManager.Subscribe_RewardCardBuffsChanged(RefreshRewardCardBuffDisplay);

        if (m_gaugeNameText != null)
            CommonUtility.SetUILocText(m_gaugeNameText, "UI_Resources");

        // RefreshVisibility는 함대상태/탐사탭 이벤트가 발생할 때만 재평가되는데, isTutorialBattle 판정은 그 이벤트들과
        // 무관하게 튜토리얼 종료 시점에 바뀜 — 종료 계기를 놓치면 다음 실제 전투까지 패널이 계속 숨겨진 채로 남을 수 있어 직접 구독
        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnTutorialCompleted += OnAnyTutorialCompleted;
    }

    void OnDestroy()
    {
        EventManager.Unsubscribe_MyFleetStateChanged(OnFleetStateChanged);
        EventManager.Unsubscribe_ExplorationTabOpened(OnExplorationTabOpened);
        EventManager.Unsubscribe_ExplorationTabClosed(OnExplorationTabClosed);
        EventManager.Unsubscribe_ZoneRunEnded(OnZoneRunEnded);
        EventManager.Unsubscribe_ResourceChanged(OnResourceChanged);
        EventManager.Unsubscribe_RewardCardBuffsChanged(RefreshRewardCardBuffDisplay);

        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnTutorialCompleted -= OnAnyTutorialCompleted;
    }

    private void RefreshRewardCardBuffDisplay()
    {
        if (m_rewardCardBuffDisplay != null)
            m_rewardCardBuffDisplay.Refresh(ObjectManager.Instance.m_rewardCardSessionState);
    }

    private void OnResourceChanged(float current, int max)
    {
        if (m_tacticPowerGauge != null)
            m_tacticPowerGauge.fillAmount = max > 0 ? Mathf.Clamp01(current / max) : 0f;

        if (m_resourceText != null)
            m_resourceText.text = $"{CommonUtility.FormatNumber(Mathf.RoundToInt(current))} / {CommonUtility.FormatNumber(max)}";
    }

    private void OnAnyTutorialCompleted(string tutorialId)
    {
        RefreshVisibility();
    }

    private void OnFleetStateChanged(EUnitState state)
    {
        m_fleetState = state;
        RefreshVisibility();
    }

    // 존런이 완전히 끝났을 때만(탈출 성공/포기 확정) 떠 있는 요격체를 정리함 — 전술 토글 설정은 유지
    private void OnZoneRunEnded()
    {
        SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
        if (myFleet == null) return;

        //myFleet.ClearAllInterceptorUnits();
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
        // Tutorial_FirstPlay_Battle(전투 연출)~Tutorial_FirstPlay_Complete(기함 폭발 후 탈출 연출)까지는 하나로 이어지는
        // 연출 구간이라 이 패널(존 정보/전술 토글 등)을 노출하지 않음 — 기함 전투 상태가 탈출 단계까지 남아있을 수 있음
        bool isTutorialBattle = TutorialActionGate.IsTutorial("Tutorial_FirstPlay_Battle")
            || TutorialActionGate.IsTutorial("Tutorial_FirstPlay_Complete");
            // || TutorialActionGate.IsTutorial("Tutorial_Exploration");

        bool shouldShow = m_fleetState.IsBattleState() == true && m_isExplorationOpen == false && isTutorialBattle == false;

        if (shouldShow == true)
            UIManager.Instance.ShowPanel(panelName);
        else
            UIManager.Instance.HidePanel(panelName);
    }

    public override void OnShowUIPanel()
    {
        // 이 패널이 실제로 스택에 push되어 뜨는 시점 = 전투가 진짜로 시작된 시점. 대치 화면(UIPanelPrepareBattle)은
        // 그 서버 응답을 기다리는 동안 스스로 정리하지 못하고 이 패널 아래 파묻힌 채 남아있으므로 여기서 걷어냄 —
        // 스택에 없거나 이미 top이면 안전하게 no-op(UIManager.RemoveHiddenPanelFromStack)
        UIManager.Instance.RemoveHiddenPanelFromStack("UIPanelPrepareBattle");
        // PVP도 같은 이유로 확인 버튼 클릭 시점엔 UIPanelRank를 pop하지 않고 남겨둠(UIPanelRank.OnBattleStartResponse 참고) — 여기서 정리
        UIManager.Instance.RemoveHiddenPanelFromStack("UIPanelRank");

        if (m_battleView != null)
            m_battleView.RefreshTacticsDisplay();

        RefreshRewardCardBuffDisplay(); // 패널이 뜨는 시점에도 1회 동기화(이후엔 OnRewardCardBuffsChanged 구독으로 갱신됨)

        CommanderInfo commanderInfo = GetCommanderInfo();
        if (commanderInfo != null)
            EventManager.Trigger_ResourceChanged(commanderInfo.resource, commanderInfo.resourceMax); // 이벤트는 값이 바뀔 때만 발행되므로 패널이 뜰 때 직접 한 번 동기화

        if (m_resourceDrainCoroutine == null)
            m_resourceDrainCoroutine = StartCoroutine(Co_DrainResource());
    }

    public override void OnHideUIPanel()
    {
        if (m_resourceDrainCoroutine != null)
        {
            StopCoroutine(m_resourceDrainCoroutine);
            m_resourceDrainCoroutine = null;
        }
    }

    private CommanderInfo GetCommanderInfo()
    {
        return DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
    }

    // 1초마다 수리/실드/요격체 틱을 시도 — 빔/미사일/함재기는 발사/발진 시점(ModuleBeam/ModuleMissile/LauncherAircraft)에서 개별 과금하므로 여기 관여하지 않음.
    // 실드는 수리/요격체와 다르게 토글과 무관하게 항상 회복을 시도함 — 토글(idx=4)은 이제 "흡수 효과 작동 여부"만 결정(SpaceShip.ApplyShieldAbsorption 참고)
    private IEnumerator Co_DrainResource()
    {
        while (true)
        {
            yield return k_oneSecondWait;

            CommanderInfo commanderInfo = GetCommanderInfo();
            SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
            if (commanderInfo == null || myFleet == null || myFleet.m_fleetInfo == null) continue;
            if (commanderInfo.resource <= 0) continue;

            int tacticOptions = myFleet.m_fleetInfo.tacticOptions;
            GameSettings gameSettings = DataManager.Instance.m_dataTableConfig.gameSettings;

            // 토글이 켜져 있어도 실질 효과가 없는 상태(체력 만땅/요격체 빈 자리 없음)면 시도 자체를 스킵 — 토글은 유지(조건이 다시 성립하면 재시도)
            bool repairHasEffect = myFleet.GetFleetHealthRatio() < 1f;
            // 방어 발동 중(게이지 소모 중)이 아니어도 아직 충전할 여지가 있으면 실질 효과를 가짐 — 게이지 0에서 충전을 시작할 수 있어야 함
            bool shieldHasEffect = myFleet.HasAnyShieldDefending() || myFleet.HasAnyShieldBelowMax();
            bool interceptorHasEffect = myFleet.HasAnyInterceptorBelowMax();

            if ((tacticOptions & (1 << 3)) != 0 && repairHasEffect == true)
            {
                int shipsNeedingRepair = myFleet.CountShipsNeedingRepair();
                if (shipsNeedingRepair > 0 && myFleet.TryChargeRepairTacticCost(shipsNeedingRepair * gameSettings.tactic.tacticRepairCost) == true)
                    myFleet.ApplyRepairTickToAllShips();
            }
            if (shieldHasEffect == true)
            {
                int shipsNeedingShield = myFleet.CountShipsNeedingShieldRegen();
                if (shipsNeedingShield > 0 && myFleet.TryChargeShieldTacticCost(shipsNeedingShield * gameSettings.tactic.tacticShieldCost) == true)
                    myFleet.ApplyShieldRegenTickToAllShips();
            }
            if ((tacticOptions & (1 << 5)) != 0 && interceptorHasEffect == true)
                myFleet.ApplyInterceptorRegenTickToAllShips(k_tacticTickSeconds); // 실제 생성 개수만큼의 과금은 ModuleInterceptor.ApplyRegenTick 내부에서 처리
        }
    }
}
