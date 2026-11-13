using System.Collections.Generic;

// 지휘관 빌드 데이터 — 함선 능력치 계산에 들어가는 "지휘관별" 입력(아키타입 패시브 진행도 + 아키타입 모듈강화 레벨)
// 내 지휘관(Commander.GetBuildData), PVP 상대 함대(SpaceFleet.m_remoteBuildData), 상대 상세 조회 결과가 모두 같은 형태로 사용 —
// 전투 스폰과 능력치 표시가 같은 계산을 거치게 하기 위한 공통 입력
public class CommanderBuildData
{
    public List<HullArchetypeProgressInfo> passiveProgress;
    public List<ArchetypeUpgradeInfo> archetypeUpgrades;

    public CommanderBuildData() { }

    public CommanderBuildData(List<HullArchetypeProgressInfo> passiveProgress, List<ArchetypeUpgradeInfo> archetypeUpgrades)
    {
        this.passiveProgress = passiveProgress;
        this.archetypeUpgrades = archetypeUpgrades;
    }

    // 아키타입 강화 효과 레벨 = 구매 횟수 + 1 — 기록이 없거나 아키타입이 비어 있으면 1(기본)
    public int GetUpgradeEffectLevel(string hullArchetype, EModuleType moduleType, int slotIndex)
    {
        if (string.IsNullOrEmpty(hullArchetype) == true) return 1;
        if (archetypeUpgrades == null) return 1;

        for (int i = 0; i < archetypeUpgrades.Count; i++)
        {
            ArchetypeUpgradeInfo entry = archetypeUpgrades[i];
            if (entry.hullArchetype != hullArchetype || entry.moduleType != moduleType || entry.slotIndex != slotIndex) continue;

            int purchaseCount = entry.level;
            return purchaseCount + 1;
        }
        return 1;
    }

    // 패시브 한 단계의 완료 여부 — 진행도 기록이 없으면 문턱 0인 단계(1단계)만 완료로 취급(서버 판정 규칙과 동일)
    public bool IsPassiveStageCompleted(string hullArchetype, int stage, int threshold)
    {
        if (passiveProgress != null)
        {
            for (int i = 0; i < passiveProgress.Count; i++)
            {
                HullArchetypeProgressInfo entry = passiveProgress[i];
                if (entry.hullArchetype == hullArchetype && entry.stage == stage)
                    return entry.completed;
            }
        }
        return threshold <= 0;
    }
}
