// 함대 전략 설정 UI — 진형 선택(라디오) + 전투 옵션 토글
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UITabFleetTactics : UITabBase
{
    [SerializeField] private Button            m_toTabFleet;
    [SerializeField] private ButtonGroupSystem m_formationButtonGroup;
    [SerializeField] private Transform         m_toggleButtonContainer;

    private Character      m_myCharacter;
    private SpaceFleet     m_myFleet;
    private ToggleButton[] m_toggleButtons;
    private bool[]         m_toggleStates;
    private Graphic[]      m_formationGroupGraphics;
    private bool           m_suppressFormationCallback;

    // 인덱스 → 진형: 0=균형, 1=공격우선, 2=방어우선
    private static readonly EFormationType[] k_formationByIndex =
    {
        EFormationType.linear_horizontal,
        EFormationType.x_offensive,
        EFormationType.x_defensive,
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
        "UITabFleetTactics_UseBattleRepair",
        "UITabFleetTactics_UseMissile",
        "UITabFleetTactics_UseAircraft",
    };

    private static readonly string[] k_toggleDescKeys =
    {
        "UITabFleetTactics_UseBattleRepair_description",
        "UITabFleetTactics_UseMissile_description",
        "UITabFleetTactics_UseAircraft_description",
    };

    void Awake()
    {
        EventManager.Subscribe_TacticToggleRequested(OnClickToggle);
    }

    void OnDestroy()
    {
        EventManager.Unsubscribe_TacticToggleRequested(OnClickToggle);
        EventManager.Unsubscribe_TacticOptionsChanged(RefreshToggleUI);
    }

    public override void InitializeUITab()
    {
        m_myCharacter = DataManager.Instance.m_currentCharacter;
        if (m_myCharacter == null || m_myCharacter.GetOwnedFleet() == null) return;
        m_myFleet = m_myCharacter.GetOwnedFleet();

        if (m_toTabFleet != null)
            m_toTabFleet.onClick.AddListener(() =>
            {
                if (m_tabSystemParent != null)
                    m_tabSystemParent.SwitchToTabByName("tab_fleet");
            });

        SetupToggleButtons();
        SetupFormationButtons();
    }

    // 서버 데이터 기준 함선 수 — 전투 중 파괴된 함선은 영향 없음
    private bool IsFormationLocked()
    {
        if (m_myFleet == null) return true;
        var ships = m_myFleet.m_fleetInfo.ships;
        return ships == null || ships.Count < 3;
    }

    private void SetupFormationButtons()
    {
        if (m_formationButtonGroup == null) return;

        m_formationButtonGroup.Initialize();

        for (int i = 0; i < k_formationByIndex.Length; i++)
        {
            int idx = i;
            EFormationType ft = k_formationByIndex[idx];
            m_formationButtonGroup.items[idx].onSelected = () =>
            {
                if (m_suppressFormationCallback == false) RequestChangeFormation(ft);
            };

            var texts = m_formationButtonGroup.items[idx].button.GetComponentsInChildren<TMP_Text>();
            if (texts.Length >= 1)
                CommonUtility.SetUILocText(texts[0], k_formationNameKeys[idx]);
            if (texts.Length >= 2)
                CommonUtility.SetUILocText(texts[1], k_formationDescKeys[idx]);
        }

        // 헤더/구분선 제외, 버튼 item 그래픽만 캐시
        var gList = new System.Collections.Generic.List<Graphic>();
        for (int i = 0; i < m_formationButtonGroup.items.Count; i++)
        {
            var item = m_formationButtonGroup.items[i];
            if (item.button != null)
                gList.AddRange(item.button.GetComponentsInChildren<Graphic>(true));
        }
        m_formationGroupGraphics = gList.ToArray();

        // 버튼 onClick을 직접 교체해 잠금 체크를 가장 먼저 수행
        for (int i = 0; i < m_formationButtonGroup.items.Count; i++)
        {
            int idx = i;
            var btn = m_formationButtonGroup.items[idx].button;
            if (btn == null) continue;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                if (IsFormationLocked() == true)
                {
                    ShowLockedAlert();
                    return;
                }
                m_formationButtonGroup.Select(idx);
            });
        }

        RefreshFormationButtonLock();
    }

    private void RefreshFormationButtonLock()
    {
        if (m_formationButtonGroup == null || m_formationGroupGraphics == null) return;

        if (IsFormationLocked() == true)
        {
            Color lockedColor = CommonUtility.PaletteColor("GeneralDark2");
            for (int i = 0; i < m_formationGroupGraphics.Length; i++)
                if (m_formationGroupGraphics[i] != null)
                    m_formationGroupGraphics[i].color = lockedColor;
        }
        else
        {
            Color inactiveColor = CommonUtility.PaletteColor("GeneralDark1");
            for (int i = 0; i < m_formationGroupGraphics.Length; i++)
                if (m_formationGroupGraphics[i] != null)
                    m_formationGroupGraphics[i].color = inactiveColor;

            int currentIdx = GetFormationIndex(m_myFleet.m_currentFormationType);
            m_suppressFormationCallback = true;
            m_formationButtonGroup.Select(currentIdx);
            m_suppressFormationCallback = false;
        }
    }

    private void ShowLockedAlert()
    {
        UIManager.Instance.ShowPopupAlert(new AlertPopupConfig
        {
            title   = LocalizationManager.Instance.Get("UITabFleetTactics_LockedTitle"),
            message = LocalizationManager.Instance.Get("UITabFleetTactics_LockedMessage"),
        });
    }

    private void RequestChangeFormation(EFormationType newFormationType)
    {
        var request = new ChangeFormationRequest
        {
            fleetId       = m_myFleet.m_fleetInfo.id,
            formationType = newFormationType
        };

        NetworkManager.Instance.ChangeFormation(request, (response) =>
        {
            if (response.errorCode == 0)
            {
                m_myFleet.UpdateShipFormation(newFormationType);
                if (response.data.updatedFleetInfo != null)
                    DataManager.Instance.SetFleetData(response.data.updatedFleetInfo);
            }
        });
    }

    private void SetupToggleButtons()
    {
        if (m_toggleButtonContainer == null) return;

        m_toggleButtons = m_toggleButtonContainer.GetComponentsInChildren<ToggleButton>();
        m_toggleStates  = new bool[m_toggleButtons.Length];

        // 0=수리(HP당 1고정), 1=미사일 평균, 2=함재기 평균
        int[] costs = { 1, GetAvgMissileCost(), GetAvgHangerCost() };

        int savedOptions = m_myFleet.m_fleetInfo.tacticOptions;
        for (int i = 0; i < m_toggleButtons.Length; i++)
        {
            m_toggleStates[i] = (savedOptions & (1 << i)) != 0;

            if (i < k_toggleNameKeys.Length)
                m_toggleButtons[i].SetTexts(k_toggleNameKeys[i], k_toggleDescKeys[i], i < costs.Length ? costs[i] : 0);

            int idx = i;
            m_toggleButtons[idx].button.onClick.AddListener(() => OnClickToggle(idx));
        }

        RefreshToggleUI(savedOptions);
        EventManager.Subscribe_TacticOptionsChanged(RefreshToggleUI);
    }

    private void RefreshToggleUI(int options)
    {
        if (m_toggleButtons == null) return;
        for (int i = 0; i < m_toggleButtons.Length; i++)
            m_toggleButtons[i].SetSelected((options & (1 << i)) != 0);
    }

    private int GetAvgMissileCost()
    {
        int total = 0, count = 0;
        foreach (var ship in m_myFleet.m_ships)
        {
            if (ship == null) continue;
            foreach (var body in ship.m_moduleBodys)
            {
                if (body == null) continue;
                foreach (var mod in body.m_missiles) { total += mod.m_mineralCost; count++; }
            }
        }
        return count == 0 ? 0 : Mathf.RoundToInt((float)total / count);
    }

    private int GetAvgHangerCost()
    {
        int total = 0, count = 0;
        foreach (var ship in m_myFleet.m_ships)
        {
            if (ship == null) continue;
            foreach (var body in ship.m_moduleBodys)
            {
                if (body == null) continue;
                foreach (var mod in body.m_hangers) { total += mod.m_mineralCost; count++; }
            }
        }
        return count == 0 ? 0 : Mathf.RoundToInt((float)total / count);
    }

    private void OnClickToggle(int idx)
    {
        if (m_myFleet == null || m_toggleStates == null || idx >= m_toggleStates.Length) return;

        m_toggleStates[idx] = !m_toggleStates[idx];

        int newOptions = 0;
        for (int i = 0; i < m_toggleStates.Length; i++)
        {
            if (m_toggleStates[i] == true)
                newOptions |= (1 << i);
        }

        // AutoRepair·TryConsumeMineral 코루틴이 즉시 참조하는 런타임 객체 갱신
        m_myFleet.m_fleetInfo.tacticOptions = newOptions;
        EventManager.Trigger_TacticOptionsChanged(newOptions); // → RefreshToggleUI 호출됨

        var req = new ChangeTacticOptionsRequest
        {
            fleetId       = m_myFleet.m_fleetInfo.id,
            tacticOptions = newOptions
        };

        NetworkManager.Instance.ChangeTacticOptions(req, (response) =>
        {
            if (response.errorCode == 0 && response.data.updatedFleetInfo != null)
            {
                DataManager.Instance.SetFleetData(response.data.updatedFleetInfo);
                // DataManager가 새 참조로 교체되므로 SpaceFleet도 재동기화
                m_myFleet.m_fleetInfo.tacticOptions = response.data.updatedFleetInfo.tacticOptions;
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

    public override void OnTabActivated()
    {
        base.OnTabActivated();
        SetOtherTabsVisible(false, includeSelf: true);
        RefreshFormationButtonLock();
    }

    public override void OnTabDeactivated()
    {
        base.OnTabDeactivated();
        SetOtherTabsVisible(true, includeSelf: true);
    }
}
