using UnityEngine;

// 함재기 약탈 — 플레이어 함재기가 적 함선에 명중할 때마다 확률로 광물을 얻음. 탐사 셀 전투(BattleStatTracker 집계 구간)에서만 판정
// 확률 = 설정 기본 확률 + 해금된 아키타입 패시브의 scavenge_chance 효과 합(self: 발사 함선 아키타입, fleet: 함대에 있는 아키타입)
public static class AircraftScavenge
{
    private const string k_effectTypeScavengeChance = "scavenge_chance";

    // 함재기 1회 명중마다 호출 — 당첨되면 광물량을 BattleStatTracker에 누적(셀 클리어 때 서버로 보고)
    public static void TryScavenge(SpaceShip sourceShip)
    {
        if (BattleStatTracker.IsCollecting() == false) return;
        if (sourceShip == null || sourceShip.m_ownerFleet == null) return;
        if (sourceShip.m_ownerFleet.m_fleetSource != EFleetSource.fleet_source_player) return;

        ScavengeSettings settings = DataManager.Instance.m_dataTableConfig.gameSettings.scavenge;
        float baseChancePercent = settings.baseChancePercent;
        float passiveBonusPercent = HullArchetypePassiveEffects.SumEffect(sourceShip, k_effectTypeScavengeChance);
        float totalChancePercent = baseChancePercent + passiveBonusPercent;

        float rollPercent = Random.value * 100f;
        if (rollPercent >= totalChancePercent) return;

        int mineralAmount = RollMineralAmount(settings);
        BattleStatTracker.AddScavengedMineral(mineralAmount);
    }

    // 최솟값부터 단위 간격으로 최댓값까지의 값 중 균등 랜덤(10~50, 단위 10 → 10/20/30/40/50)
    private static int RollMineralAmount(ScavengeSettings settings)
    {
        int step = Mathf.Max(settings.mineralStep, 1);
        int stepCount = (settings.mineralMax - settings.mineralMin) / step + 1;
        int stepIndex = Random.Range(0, Mathf.Max(stepCount, 1));
        return settings.mineralMin + stepIndex * step;
    }
}
