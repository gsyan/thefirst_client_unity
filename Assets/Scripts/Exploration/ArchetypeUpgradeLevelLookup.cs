// 아키타입 단위 영구 모듈강화(M6) 레벨 조회 — 함선 인스턴스가 아니라 (지휘관, hullArchetype, moduleType, slotIndex) 단위로 공유되는 레벨
// beam/missile/hangar는 슬롯마다 레벨이 독립(slotIndex 전달 필요) — hull/shield/interceptor는 슬롯 개념이 없어 slotIndex=0(기본값) 고정
// DB에는 "구매 횟수"(0부터 시작)로 저장되고, 효과 레벨 = 구매횟수+1 — 0회 구매 시 효과레벨 1 = 기본 장착 subType("_1_1")과 동일한 데이터이므로 별도 분기 불필요
public static class ArchetypeUpgradeLevelLookup
{
    // 내 지휘관 기준 레벨 — 함대 맥락이 없는 UI 미리보기/편성 화면용. 내 커맨더가 없으면 기본 효과레벨(1)
    public static int GetLevel(string hullArchetype, EModuleType moduleType, int slotIndex = 0)
    {
        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander == null) return 1;

        return GetLevel(commander.GetBuildData(), hullArchetype, moduleType, slotIndex);
    }

    // 함대 기준 레벨 — 모듈/함선이 속한 함대의 지휘관 빌드 데이터를 따름(내 함대=내 지휘관, PVP 상대=받은 데이터, 존 적/시네마틱=강화 없음 1)
    // ownerFleet이 null이면 내 지휘관 기준(함대 맥락 없는 호출)
    public static int GetLevel(SpaceFleet ownerFleet, string hullArchetype, EModuleType moduleType, int slotIndex = 0)
    {
        if (ownerFleet == null) return GetLevel(hullArchetype, moduleType, slotIndex);

        return GetLevel(ownerFleet.GetBuildData(), hullArchetype, moduleType, slotIndex);
    }

    // 빌드 데이터 기준 레벨 — null이면 강화 없음(1)
    public static int GetLevel(CommanderBuildData buildData, string hullArchetype, EModuleType moduleType, int slotIndex = 0)
    {
        if (buildData == null) return 1;
        return buildData.GetUpgradeEffectLevel(hullArchetype, moduleType, slotIndex);
    }
}
