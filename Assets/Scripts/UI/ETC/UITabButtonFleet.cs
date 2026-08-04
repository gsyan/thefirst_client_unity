// 함대 전투력 요약 바 — Attack/HP 표시, 클릭 시 전체 스탯 팝업
using UnityEngine;
using UnityEngine.UI;

public class UITabButtonFleet : MonoBehaviour
{
    [SerializeField] private RectTransform m_rtFleetStats;
    [SerializeField] private RectTransform m_rtFleetTactics;

    private static readonly string[] k_tacticSprites = { "auto-repair", "missile-pod", "jet-fighter" };

    private RowImageText[] m_fleetStatRows;
    private Image[]        m_tacticIcons;
    private SpaceFleet     m_fleet;

    private void Awake()
    {
        m_fleetStatRows = m_rtFleetStats.GetComponentsInChildren<RowImageText>();
        m_tacticIcons   = m_rtFleetTactics.GetComponentsInChildren<Image>();
        for (int i = 0; i < m_tacticIcons.Length && i < k_tacticSprites.Length; i++)
        {
            Sprite sp = UISpriteCache.Get(k_tacticSprites[i]);
            if (sp != null) m_tacticIcons[i].sprite = sp;
        }
    }

    private void Start()
    {
        EventManager.Subscribe_ShipStatsChanged(OnShipStatsChanged);
        EventManager.Subscribe_FleetUpdateHP(RefreshText);
        EventManager.Subscribe_TacticOptionsChanged(RefreshTactics);
        // 탭 초기화 시점에 함대가 아직 스폰되지 않았을 수 있음(튜토리얼 등) — 스폰/교체(튜토리얼→실제 함대 전환 포함) 시점에 뒤늦게 바인딩
        EventManager.Subscribe_MyFleetSet(OnMyFleetSet);

        // 이미 함대가 존재하면 즉시 바인딩
        if (DataManager.Instance.m_currentCommander != null && ObjectManager.Instance.GetMyFleet() != null)
            BindPlayerFleet();
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_ShipStatsChanged(OnShipStatsChanged);
        EventManager.Unsubscribe_FleetUpdateHP(RefreshText);
        EventManager.Unsubscribe_TacticOptionsChanged(RefreshTactics);
        EventManager.Unsubscribe_MyFleetSet(OnMyFleetSet);
    }

    // 함대 스폰/교체(튜토리얼→실제 함대 전환 포함) 시 호출 — 파괴된 이전 함대 참조가 고착되지 않도록 재바인딩
    private void OnMyFleetSet()
    {
        BindPlayerFleet();
    }

    private void BindPlayerFleet()
    {
        m_fleet = ObjectManager.Instance.GetMyFleet();
        if (m_fleet == null) return;

        RefreshText();
        // 전술 옵션 기능 제거됨 — 아이콘은 항상 비활성 표시로 고정
        RefreshTactics(0);
    }

    private void OnShipStatsChanged(SpaceShip ship) => RefreshText();

    private void RefreshTactics(int tacticOptions)
    {
        if (m_tacticIcons == null) return;
        Color bright = CommonUtility.PaletteColor("General.Bright1");
        Color dark   = CommonUtility.PaletteColor("General.Dark1");
        for (int i = 0; i < m_tacticIcons.Length; i++)
            m_tacticIcons[i].color = (tacticOptions & (1 << i)) != 0 ? bright : dark;
    }

    private void RefreshText()
    {
        if (m_fleet == null) return;

        //CapabilityProfile cur = m_fleet.GetFleetCapabilityProfile(true);
        CapabilityProfile org = m_fleet.GetFleetCapabilityProfile(false);

        m_fleetStatRows[0].SetTextWithString(CommonUtility.FormatBigNumber(org.attack));
        m_fleetStatRows[1].SetTextWithString(CommonUtility.FormatBigNumber(org.health));

        for (int i = 0; i < m_fleetStatRows.Length; i++)
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_fleetStatRows[i].transform as RectTransform);
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_rtFleetStats);
    }

}
