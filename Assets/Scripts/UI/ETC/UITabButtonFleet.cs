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
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;

        m_fleet = character.GetOwnedFleet();
        if (m_fleet == null) return;

        EventManager.Subscribe_ShipStatsChanged(OnShipStatsChanged);
        EventManager.Subscribe_FleetUpdateHP(RefreshText);
        EventManager.Subscribe_TacticOptionsChanged(RefreshTactics);

        RefreshText();
        RefreshTactics(m_fleet.m_fleetInfo.tacticOptions);
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_ShipStatsChanged(OnShipStatsChanged);
        EventManager.Unsubscribe_FleetUpdateHP(RefreshText);
        EventManager.Unsubscribe_TacticOptionsChanged(RefreshTactics);
    }

    private void OnShipStatsChanged(SpaceShip ship) => RefreshText();

    private void RefreshTactics(int tacticOptions)
    {
        if (m_tacticIcons == null) return;
        Color bright = CommonUtility.PaletteColor("GeneralBright1");
        Color dark   = CommonUtility.PaletteColor("GeneralDark1");
        for (int i = 0; i < m_tacticIcons.Length; i++)
            m_tacticIcons[i].color = (tacticOptions & (1 << i)) != 0 ? bright : dark;
    }

    private void RefreshText()
    {
        if (m_fleet == null) return;

        //CapabilityProfile cur = m_fleet.GetFleetCapabilityProfile(true);
        CapabilityProfile org = m_fleet.GetFleetCapabilityProfile(false);

        m_fleetStatRows[0].SetTextWithString($"{org.attack:F0}");
        m_fleetStatRows[1].SetTextWithString($"{org.health:F0}");

        for (int i = 0; i < m_fleetStatRows.Length; i++)
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_fleetStatRows[i].transform as RectTransform);
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_rtFleetStats);
    }

    // private void OnInfoClicked()
    // {
    //     if (m_fleet == null) return;

    //     CapabilityProfile org = m_fleet.GetFleetCapabilityProfile(false);
    //     CapabilityProfile cur = m_fleet.GetFleetCapabilityProfile(true);

    //     var sb = new StringBuilder();
    //     sb.AppendLine($"{CommonUtility.Sprite("bubbling-beam")} (Attack)  {cur.attack:F0}");
    //     sb.Append($"{CommonUtility.Sprite("techno-heart")} (HP)  {org.health:F0} ");
    //     sb.AppendLine($"{CommonUtility.Sprite("auto-repair")} (Repair)  {cur.repair:F0}");
    //     sb.AppendLine($"{CommonUtility.Sprite("rocket-thruster")} (Speed)  {cur.speed:F0}");
        

    //     if (org.airCount > 0)
    //     {
    //         sb.AppendLine();
    //         sb.AppendLine($"{CommonUtility.Sprite("strafe")} (Aircraft Attack)  {cur.airAttack:F0}");
    //         sb.Append    ($"{CommonUtility.Sprite("jet-fighter")} (Aircraft)  {org.airCount:F0}");
    //     }

    //     UIManager.Instance.ShowPopupAlert("Fleet Stats", sb.ToString(), null);
    // }
}
