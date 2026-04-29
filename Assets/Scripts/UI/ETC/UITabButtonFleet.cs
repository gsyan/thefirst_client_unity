// 함대 전투력 요약 바 — Attack/HP 표시, 클릭 시 전체 스탯 팝업
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UITabButtonFleet : MonoBehaviour
{
    [SerializeField] private TMP_Text m_textAttack;
    [SerializeField] private TMP_Text m_textHp;
    
    private SpaceFleet m_fleet;

    private void Start()
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;

        m_fleet = character.GetOwnedFleet();
        if (m_fleet == null) return;

        EventManager.Subscribe_ShipStatsChanged(OnShipStatsChanged);
        EventManager.Subscribe_FleetUpdateHP(RefreshText);

        RefreshText();
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_ShipStatsChanged(OnShipStatsChanged);
        EventManager.Unsubscribe_FleetUpdateHP(RefreshText);
    }

    private void OnShipStatsChanged(SpaceShip ship) => RefreshText();

    private void RefreshText()
    {
        if (m_fleet == null) return;

        //CapabilityProfile cur = m_fleet.GetFleetCapabilityProfile(true);
        CapabilityProfile org = m_fleet.GetFleetCapabilityProfile(false);

        m_textAttack.text = $"{org.attack:F0}";
        m_textHp.text =$"{org.health:F0}";

        LayoutRebuilder.ForceRebuildLayoutImmediate(m_textAttack.transform.parent as RectTransform);
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
