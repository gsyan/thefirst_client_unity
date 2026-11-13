using UnityEngine;

// 탐사(argus) — 전투 중 탐사 능력이 있는 함선이 일정 간격으로 슬롯마다 독립 확률 판정(SpaceShip.Co_Scout가 간격마다 호출)
// 확률 = 패시브 scout_chance 효과 합(self: 그 함선 아키타입, fleet: 함대 전체 부여), 슬롯 수 = 1 + 패시브 scout_slot 효과 합
// 확률이 0이면 탐사 능력이 없는 함선 — 성공하면 광물을 BattleStatTracker에 누적하고(셀 클리어 때 서버로 보고) 성공 위치에 이펙트 재생
public static class ShipScout
{
    private const string k_effectScoutChance = "scout_chance";
    private const string k_effectScoutSlot = "scout_slot";

    // 광물량 단계(최솟값부터 단위 간격 순서)별 이펙트 색 — 단계 수가 이보다 많으면 마지막 색 사용
    private static readonly Color[] k_amountTierColors =
    {
        new Color(0.85f, 0.9f, 1f),    // 1단계: 흰푸른색
        new Color(0.45f, 1f, 0.55f),   // 2단계: 초록
        new Color(0.35f, 0.85f, 1f),   // 3단계: 하늘
        new Color(0.8f, 0.5f, 1f),     // 4단계: 보라
        new Color(1f, 0.85f, 0.25f),   // 5단계: 금색
    };

    // 탐사 실패 이펙트 색 — 성공 단계 색들과 구분되도록 붉은 계열(숫자 텍스트 없이 파티클만 재생)
    private static readonly Color k_failColor = new Color(1f, 0.3f, 0.25f);

    public static void RollScout(SpaceShip ship)
    {
        if (BattleStatTracker.IsCollecting() == false) return;
        if (ship == null || ship.m_ownerFleet == null) return;
        if (ship.m_ownerFleet.m_fleetSource != EFleetSource.fleet_source_player) return;

        float chancePercent = HullArchetypePassiveEffects.SumEffect(ship, k_effectScoutChance);
        if (chancePercent <= 0f) return;

        float extraSlotValue = HullArchetypePassiveEffects.SumEffect(ship, k_effectScoutSlot);
        int slotCount = 1 + Mathf.RoundToInt(extraSlotValue);

        ScoutSettings settings = DataManager.Instance.m_dataTableConfig.gameSettings.scout;
        for (int slot = 0; slot < slotCount; slot++)
        {
            float rollPercent = Random.value * 100f;
            bool isSuccess = rollPercent < chancePercent;
            if (isSuccess == false)
            {
                PlayScoutEffect(ship, k_failColor, 0); // 실패도 판정이 있었음을 보여주는 이펙트(숫자 없음)
                continue;
            }

            int stepIndex = RollStepIndex(settings);
            int mineralAmount = settings.mineralMin + stepIndex * Mathf.Max(settings.mineralStep, 1);

            BattleStatTracker.AddScoutedMineral(mineralAmount);
            BattleStatTracker.AddShipArchetypeCounter(ship, BattleStatTracker.k_counterScoutSuccess, 1f);

            int colorIndex = Mathf.Min(stepIndex, k_amountTierColors.Length - 1);
            PlayScoutEffect(ship, k_amountTierColors[colorIndex], mineralAmount);
        }
    }

    // 최솟값부터 단위 간격 단계 중 균등 랜덤 — 반환값은 단계 번호(0부터)
    private static int RollStepIndex(ScoutSettings settings)
    {
        int step = Mathf.Max(settings.mineralStep, 1);
        int stepCount = (settings.mineralMax - settings.mineralMin) / step + 1;
        return Random.Range(0, Mathf.Max(stepCount, 1));
    }

    // 함선 주변 3D 공간의 무작위 지점에 이펙트 재생 — 풀이 없거나 프리팹이 없으면 이펙트만 생략(광물 지급은 그대로)
    // mineralAmount가 0이면 실패 이펙트(숫자 텍스트 없음)
    private static void PlayScoutEffect(SpaceShip ship, Color color, int mineralAmount)
    {
        EffectScout effect = ObjectManager.Instance.m_poolManager.Get<EffectScout>(EPoolName.EFFECT_SCOUT);
        if (effect == null) return;

        Bounds shipBounds = ship.CalculateShipBounds();
        float spreadRadius = shipBounds.extents.magnitude * 1.2f;
        Vector3 effectPosition = shipBounds.center + Random.insideUnitSphere * spreadRadius;

        effect.PlayAt(effectPosition, color, mineralAmount);
    }
}
