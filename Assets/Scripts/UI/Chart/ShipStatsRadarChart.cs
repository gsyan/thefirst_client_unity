using UnityEngine;
using TMPro;

public class ShipStatsRadarChart : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SimpleRadarChart radarChart;

    [Header("Labels")]
    [SerializeField] private TMP_Text firepowerLabel;
    [SerializeField] private TMP_Text defenseLabel;
    [SerializeField] private TMP_Text speedLabel;
    [SerializeField] private TMP_Text hpLabel;
    [SerializeField] private TMP_Text shieldLabel;
    [SerializeField] private TMP_Text energyLabel;

    /// <summary>
    /// 함선 스탯 업데이트
    /// </summary>
    public void UpdateShipStats(CapabilityProfile stats)
    {
        if (radarChart == null)
            radarChart = GetComponent<SimpleRadarChart>();

        // 차트 업데이트
        radarChart.SetRadarChartStats(stats);

        // 라벨 업데이트
        // if (firepowerLabel != null) firepowerLabel.text = $"Attack: {stats.attack:F0}";
        // if (defenseLabel != null) defenseLabel.text = $"Defense: {stats.defense:F0}";
        // if (speedLabel != null) speedLabel.text = $"Speed: {stats.speed:F0}";
        // if (hpLabel != null) hpLabel.text = $"HP: {stats.hp:F0}";
        // if (shieldLabel != null) shieldLabel.text = $"Shield: {stats.shield:F0}";
        // if (energyLabel != null) energyLabel.text = $"Energy: {stats.energy:F0}";
    }

    /// <summary>
    /// ShipInfo로부터 직접 업데이트
    /// </summary>
    public void UpdateFromShipInfo(ShipInfo shipInfo)
    {
        if (shipInfo == null) return;
        CapabilityProfile profile = CommonUtility.GetShipCapabilityProfile(shipInfo);
        UpdateShipStats(profile);
    }
}