// 아키타입 단위 영구 모듈강화(M6) 레벨 조회 — 함선 인스턴스가 아니라 (내 커맨더, hullArchetype, moduleType, slotIndex) 단위로 공유되는 레벨
// beam/missile/hangar는 슬롯마다 레벨이 독립(slotIndex 전달 필요) — hull/shield/interceptor는 슬롯 개념이 없어 slotIndex=0(기본값) 고정
// DB에는 "구매 횟수"(0부터 시작)로 저장되고, 효과 레벨 = 구매횟수+1 — 0회 구매 시 효과레벨 1 = 기본 장착 subType("_1_1")과 동일한 데이터이므로 별도 분기 불필요
public static class ArchetypeUpgradeLevelLookup
{
    // hullArchetype이 비어있거나 내 커맨더가 없으면(적/시네마틱 함대 등) 기본 효과레벨(1)
    public static int GetLevel(string hullArchetype, EModuleType moduleType, int slotIndex = 0)
    {
        if (string.IsNullOrEmpty(hullArchetype) == true) return 1;

        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander == null) return 1;

        int purchaseCount = commander.GetArchetypeUpgradeLevel(hullArchetype, moduleType, slotIndex);
        return purchaseCount + 1;
    }
}
