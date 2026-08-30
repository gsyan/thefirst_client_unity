// 함대 전략 설정 UI — 진형 선택(라디오) + 전투 옵션 토글
#if false
using UnityEngine;
using UnityEngine.UI;

public class UITabFleetTactics : UITabBase
{
    [SerializeField] private Button    m_toTabFleet;
    [SerializeField] private Transform m_toggleButtonContainer;
    [SerializeField] private Transform m_formationButtonContainer;

    private SpaceFleet     m_playerFleet;
    private ToggleButton[] m_toggleButtons;
    private bool[]         m_toggleStates;
    private ToggleButton[] m_formationButtons;
    private int            m_selectedFormationIdx = -1;
    private bool           m_suppressFormationCallback;

    // 인덱스 → 진형: 0=균형, 1=공격우선, 2=방어우선
    private static readonly EFormationType[] k_formationByIndex =
    {
        EFormationType.linear_horizontal,
        EFormationType.x,
    };

    private static readonly string[] k_formationNameKeys =
    {
        "UITabFleetTactics_FormationType_LinearHorizontal",
        "UITabFleetTactics_FormationType_XOffensive",
        "UITabFleetTactics_FormationType_XDefensive",
    };

    private static readonly string[] k_formationDescKeys =
    {
        "UITabFleetTactics_FormationType_LinearHorizontal_description",
        "UITabFleetTactics_FormationType_XOffensive_description",
        "UITabFleetTactics_FormationType_XDefensive_description",
    };

    private static readonly string[] k_toggleNameKeys =
    {
        "UITabFleetTactics_RepairBoost",
        "UITabFleetTactics_MissileBoost",
        "UITabFleetTactics_AircraftBoost",
    };

    private static readonly string[] k_toggleDescKeys =
    {
        "UITabFleetTactics_RepairBoost_description",
        "UITabFleetTactics_MissileBoost_description",
        "UITabFleetTactics_AircraftBoost_description",
    };

    void Awake()
    {
        EventManager.Subscribe_TacticToggleRequested(OnClickToggle);
        EventManager.Subscribe_FormationChanged(OnExternalFormationChanged);
    }

    void OnDestroy()
    {
        EventManager.Unsubscribe_TacticToggleRequested(OnClickToggle);
        EventManager.Unsubscribe_TacticOptionsChanged(RefreshToggleUI);
        EventManager.Unsubscribe_FormationChanged(OnExternalFormationChanged);
        EventManager.Unsubscribe_MyFleetSet(OnMyFleetSet);
    }

    public override void InitializeUITab()
    {
        if (DataManager.Instance.m_currentCommander == null) return;

        if (m_toTabFleet != null)
            m_toTabFleet.onClick.AddListener(() =>
            {
                if (m_tabSystemParent != null)
                    m_tabSystemParent.SwitchToTabByName("tab_fleet");
            });

        EventManager.Subscribe_MyFleetSet(OnMyFleetSet);

        BindPlayerFleet();
    }

    // 함대 스폰/교체(튜토리얼→실제 함대 전환 포함) 시 호출 — 매번 탭 열 때 체크하지 않아도 되도록 이벤트로 처리
    private void OnMyFleetSet()
    {
        BindPlayerFleet();
    }

    private void BindPlayerFleet()
    {
        m_playerFleet = ObjectManager.Instance.GetMyFleet();
        if (m_playerFleet == null) return;

        SetupToggleButtons();
        SetupFormationButtons();
    }

    public override void OnTabActivated()
    {
        base.OnTabActivated();
        HideTabButtons();
        RefreshFormationButtonLock();
    }

    public override void OnTabDeactivated()
    {
        base.OnTabDeactivated();
        RefreshTabButtons();
    }

    private void SetupToggleButtons()
    {
        if (m_toggleButtonContainer == null) return;

        m_toggleButtons = m_toggleButtonContainer.GetComponentsInChildren<ToggleButton>();
        m_toggleStates  = new bool[m_toggleButtons.Length];

        var gs = DataManager.Instance.m_dataTableConfig.gameSettings;
        int[] k_toggleDescArgs =
        {
            gs.repairBoostExplorationPointPerSec,
            gs.missileTacticExplorationPointPerSec,
            gs.aircraftTacticExplorationPointPerSec,
        };

        int savedOptions = m_playerFleet.m_fleetInfo.tacticOptions;
        for (int i = 0; i < m_toggleButtons.Length; i++)
        {
            m_toggleStates[i] = (savedOptions & (1 << i)) != 0;

            if (i < k_toggleNameKeys.Length)
                m_toggleButtons[i].SetTexts(k_toggleNameKeys[i], k_toggleDescKeys[i], k_toggleDescArgs[i]);

            int idx = i;
            m_toggleButtons[idx].button.onClick.RemoveAllListeners();
            m_toggleButtons[idx].button.onClick.AddListener(() => OnClickToggle(idx));
        }

        RefreshToggleUI(savedOptions);
        // 함대 재바인딩 시(튜토리얼→실제 함대 전환 등) 중복 구독 방지
        EventManager.Unsubscribe_TacticOptionsChanged(RefreshToggleUI);
        EventManager.Subscribe_TacticOptionsChanged(RefreshToggleUI);
    }

    private void RefreshToggleUI(int options)
    {
        if (m_toggleButtons == null) return;
        for (int i = 0; i < m_toggleButtons.Length; i++)
            m_toggleButtons[i].SetSelected((options & (1 << i)) != 0);
    }

    private void OnClickToggle(int idx)
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_playerFleet == null || m_toggleStates == null || idx >= m_toggleStates.Length) return;

        m_toggleStates[idx] = !m_toggleStates[idx];

        int newOptions = 0;
        for (int i = 0; i < m_toggleStates.Length; i++)
        {
            if (m_toggleStates[i] == true)
                newOptions |= (1 << i);
        }

        // AutoRepair·TryConsumeMineral 코루틴이 즉시 참조하는 런타임 객체 갱신
        m_playerFleet.m_fleetInfo.tacticOptions = newOptions;
        EventManager.Trigger_TacticOptionsChanged(newOptions); // → RefreshToggleUI 호출됨

        var req = new ChangeTacticOptionsRequest
        {
            fleetId       = m_playerFleet.m_fleetInfo.id,
            tacticOptions = newOptions
        };

        NetworkManager.Instance.ChangeTacticOptions(req, (response) =>
        {
            if (response.errorCode == 0)
            {
                DataManager.Instance.ApplyFleetTacticOptions(response.data.tacticOptions);
            }
        });
    }


    // 서버 데이터 기준 함선 수 — 전투 중 파괴된 함선은 영향 없음
    private bool IsFormationLocked()
    {
        if (m_playerFleet == null) return true;
        var ships = m_playerFleet.m_fleetInfo.ships;
        return ships == null || ships.Count < 3;
    }

    private void SetupFormationButtons()
    {
        if (m_formationButtonContainer == null) return;

        m_formationButtons = m_formationButtonContainer.GetComponentsInChildren<ToggleButton>();

        for (int i = 0; i < m_formationButtons.Length; i++)
            m_formationButtons[i].SetSelected(false);

        for (int i = 0; i < m_formationButtons.Length; i++)
        {
            if (i < k_formationNameKeys.Length)
                m_formationButtons[i].SetTexts(k_formationNameKeys[i], k_formationDescKeys[i]);

            int idx = i;
            m_formationButtons[idx].button.onClick.RemoveAllListeners();
            m_formationButtons[idx].button.onClick.AddListener(() =>
            {
                if (IsFormationLocked() == true)
                {
                    ShowLockedAlert();
                    return;
                }
                SelectFormation(idx);
            });
        }

        RefreshFormationButtonLock();
    }

    private void SelectFormation(int idx)
    {
        if (m_formationButtons == null || idx < 0 || idx >= m_formationButtons.Length) return;
        if (idx == m_selectedFormationIdx) return;

        if (m_selectedFormationIdx >= 0 && m_selectedFormationIdx < m_formationButtons.Length)
            m_formationButtons[m_selectedFormationIdx].SetSelected(false);

        m_selectedFormationIdx = idx;
        m_formationButtons[idx].SetSelected(true);

        if (m_suppressFormationCallback == false)
            RequestChangeFormation(k_formationByIndex[idx]);
    }

    // 잠김 상태에서는 색상을 Locked로 표시하고, 기본 진형(균형=index 0)에 체크마크를 초기값으로 표시
    private const int LOCKED_DEFAULT_FORMATION_IDX = 0;

    private void RefreshFormationButtonLock()
    {
        if (m_formationButtons == null) return;

        if (IsFormationLocked() == true)
        {
            for (int i = 0; i < m_formationButtons.Length; i++)
                m_formationButtons[i].SetLockedVisual(i == LOCKED_DEFAULT_FORMATION_IDX);
            m_selectedFormationIdx = -1;
        }
        else
        {
            // 잠김→해제 전환 시 Locked 색이 남아있는 버튼들을 정상 색으로 복원
            for (int i = 0; i < m_formationButtons.Length; i++)
                m_formationButtons[i].SetSelected(false);

            // SetSelected(false)로 체크마크를 껐으므로, SelectFormation의 "이미 선택된 idx면 무시" 가드에 막히지 않도록 상태 초기화
            m_selectedFormationIdx = -1;

            int currentIdx = GetFormationIndex(m_playerFleet.m_currentFormationType);
            m_suppressFormationCallback = true;
            SelectFormation(currentIdx);
            m_suppressFormationCallback = false;
        }
    }

    private void ShowLockedAlert()
    {
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            title        = LocalizationManager.Instance.Get("UITabFleetTactics_LockedTitle"),
            message      = LocalizationManager.Instance.Get("UITabFleetTactics_LockedMessage"),
            autoCloseSec = 5f,
        });
    }

    private void RequestChangeFormation(EFormationType newFormationType)
    {
        var request = new ChangeFormationRequest
        {
            fleetId       = m_playerFleet.m_fleetInfo.id,
            formationType = newFormationType
        };

        NetworkManager.Instance.ChangeFormation(request, (response) =>
        {
            if (response.errorCode == 0)
            {
                m_playerFleet.UpdateShipFormation(newFormationType, bSmooth: true);
                DataManager.Instance.ApplyFleetFormation(response.data.formation);
            }
        });
    }

    

    private static int GetFormationIndex(EFormationType ft)
    {
        for (int i = 0; i < k_formationByIndex.Length; i++)
        {
            if (k_formationByIndex[i] == ft) return i;
        }
        return 0;
    }

    // 외부(UIBattleView 버튼 등)에서 진형이 변경되면 버튼 선택 상태 동기화
    private void OnExternalFormationChanged(EFormationType formation)
    {
        if (m_formationButtons == null) return;
        int idx = GetFormationIndex(formation);
        m_suppressFormationCallback = true;
        SelectFormation(idx);
        m_suppressFormationCallback = false;
    }
}
#endif

