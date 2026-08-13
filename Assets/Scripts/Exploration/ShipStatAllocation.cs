// 함선 프리셋의 성능포인트 배분 입력값(총량은 프리셋마다 가변) — 디자이너가 프리셋 제작 시 채우는 값
// 장착 코스트/기본값 출처: DataTableConfig.gameSettings.shipStatFormula (ShipStatFormulaSettings)
// 카테고리별 슬롯 상한(maxModuleSlots)은 DataTableConfig에서 관리 — 이 클래스는 배열 길이 그대로 사용
// 슬롯 장착 여부는 별도 bool 배열로 관리(CSV 빈 칸 = 미장착)
// Docs/Exploration_Revamp.md §1-1(장착+강화), §1-4(실드/요격체) 참고
using System.Collections.Generic;

[System.Serializable]
public class ShipStatAllocation
{
    // Flat Stats — 장착 개념 없이 순수 포인트 배분. 기본값/계수 미확정 — 임시 1p=+0.1
    public int healthPoints;
    public int turnRatePoints;
    public int repairPoints;

    // Beam — 슬롯당 장착 서브타입(EModuleSubType 이름, 예: beam_t1_m1) + 속성별 강화 포인트(공격력/연사력/발사체속도). 빈 문자열 = 미장착
    public string[] beamModuleSubType = new string[0];
    public int[] beamAttackPoints = new int[0];
    public int[] beamFireRatePoints = new int[0];
    public int[] beamProjectileSpeedPoints = new int[0];

    // Missile — 빔과 동일 구조 + 적중 시 대상 무장 침묵 시간 강화(미사일 전용)
    public string[] missileModuleSubType = new string[0];
    public int[] missileAttackPoints = new int[0];
    public int[] missileFireRatePoints = new int[0];
    public int[] missileProjectileSpeedPoints = new int[0];
    public int[] missileSilencePoints = new int[0];

    // Hangar — 슬롯당 장착 서브타입 + 4개 서브스탯
    public string[] hangarModuleSubType = new string[0];
    public int[] hangarShipAttackPoints = new int[0];
    public int[] hangarFighterAttackPoints = new int[0];
    public int[] hangarAmmoPoints = new int[0];
    public int[] hangarHealthPoints = new int[0];

    // Interceptor — 슬롯당 장착 서브타입(예: interceptor_t1_m1) + 2개 서브스탯. 빈 문자열 = 미장착. 장착 코스트는 DataTableModule의 해당 subType(level 1) cost_mp
    public string[] interceptorModuleSubType = new string[0];
    public int[] interceptorDelayPoints = new int[0];
    public int[] interceptorRegenRatePoints = new int[0];

    // Shield — 장착 서브타입(예: shield_t1_m1). 빈 문자열 = 미장착. 코스트는 DataTableModule의 해당 subType(level 1) cost_mp. 강화 서브스탯 3종은 1p=1선택
    public string shieldModuleSubType = "";
    public int shieldGaugePoints;
    public int shieldDelayPoints;
    public int shieldRegenRatePoints; // 회복속도(초당 게이지 회복량)

    // 슬롯 설치 코스트는 티어(subType)마다 다름 — DataTableModule.GetModuleDataFromTable(subType).statPoint 조회
    // bodyPrefabName: 함체(prefabName) 자체의 설치 비용도 지휘력에 포함시키기 위한 body subType 이름(예: body_t1_m1). 생략 시 0으로 취급
    public int GetTotalPointsUsed(DataTableModule moduleTable, string bodyPrefabName = "")
    {
        int total = healthPoints + turnRatePoints + repairPoints;
        if (string.IsNullOrEmpty(bodyPrefabName) == false)
            total += GetInstallCost(moduleTable, bodyPrefabName);

        for (int i = 0; i < beamModuleSubType.Length; i++)
        {
            if (string.IsNullOrEmpty(beamModuleSubType[i]) == false)
                total += GetInstallCost(moduleTable, beamModuleSubType[i]) + GetAt(beamAttackPoints, i) + GetAt(beamFireRatePoints, i) + GetAt(beamProjectileSpeedPoints, i);
        }

        for (int i = 0; i < missileModuleSubType.Length; i++)
        {
            if (string.IsNullOrEmpty(missileModuleSubType[i]) == false)
                total += GetInstallCost(moduleTable, missileModuleSubType[i]) + GetAt(missileAttackPoints, i) + GetAt(missileFireRatePoints, i) + GetAt(missileProjectileSpeedPoints, i) + GetAt(missileSilencePoints, i);
        }

        for (int i = 0; i < hangarModuleSubType.Length; i++)
        {
            if (string.IsNullOrEmpty(hangarModuleSubType[i]) == false)
                total += GetInstallCost(moduleTable, hangarModuleSubType[i]) + hangarShipAttackPoints[i] + hangarFighterAttackPoints[i] + hangarAmmoPoints[i] + hangarHealthPoints[i];
        }

        for (int i = 0; i < interceptorModuleSubType.Length; i++)
        {
            if (string.IsNullOrEmpty(interceptorModuleSubType[i]) == false)
                total += GetInstallCost(moduleTable, interceptorModuleSubType[i]) + interceptorDelayPoints[i] + interceptorRegenRatePoints[i];
        }

        if (string.IsNullOrEmpty(shieldModuleSubType) == false)
            total += GetInstallCost(moduleTable, shieldModuleSubType) + shieldGaugePoints + shieldDelayPoints + shieldRegenRatePoints;

        return total;
    }

    // 신규 추가 필드는 기존 프리셋 데이터에서 배열 크기가 subType 배열과 다를 수 있어 범위를 벗어나면 0으로 취급
    private static int GetAt(int[] array, int index)
    {
        return index < array.Length ? array[index] : 0;
    }

    private static int GetInstallCost(DataTableModule moduleTable, string subTypeName)
    {
        if (moduleTable == null) return 0;
        if (System.Enum.TryParse(subTypeName, out EModuleSubType subType) == false) return 0;
        ModuleData data = moduleTable.GetModuleDataFromTable(subType);
        return data != null ? data.statPoint : 0;
    }

    // 커스터마이징된 함선 로드아웃(ShipInfo.bodies, on/off만 지원) → 전투 계산용 ShipStatAllocation 조립
    // baseAllocation은 카테고리별 슬롯 배열 크기(=maxModuleSlots)의 출처로만 쓰고, 실제 장착 여부/서브타입은 bodies로 전부 덮어씀
    // (on/off만 지원하므로 강화 포인트는 항상 0 — baseAllocation의 강화 포인트도 어차피 전부 0이라 별도 복사 불필요)
    public static ShipStatAllocation BuildFromModuleBodyInfo(ShipStatAllocation baseAllocation, ModuleBodyInfo bodies)
    {
        if (bodies == null) return baseAllocation;

        var result = new ShipStatAllocation
        {
            healthPoints = baseAllocation.healthPoints,
            turnRatePoints = baseAllocation.turnRatePoints,
            repairPoints = baseAllocation.repairPoints,
            shieldModuleSubType = baseAllocation.shieldModuleSubType,
            shieldGaugePoints = baseAllocation.shieldGaugePoints,
            shieldDelayPoints = baseAllocation.shieldDelayPoints,
            shieldRegenRatePoints = baseAllocation.shieldRegenRatePoints,
            interceptorModuleSubType = baseAllocation.interceptorModuleSubType,
            interceptorDelayPoints = baseAllocation.interceptorDelayPoints,
            interceptorRegenRatePoints = baseAllocation.interceptorRegenRatePoints,
        };

        result.beamModuleSubType = new string[baseAllocation.beamModuleSubType.Length];
        result.beamAttackPoints = new int[baseAllocation.beamModuleSubType.Length];
        result.beamFireRatePoints = new int[baseAllocation.beamModuleSubType.Length];
        result.beamProjectileSpeedPoints = new int[baseAllocation.beamModuleSubType.Length];
        ApplyModulesToSlots(result.beamModuleSubType, bodies.beams);

        result.missileModuleSubType = new string[baseAllocation.missileModuleSubType.Length];
        result.missileAttackPoints = new int[baseAllocation.missileModuleSubType.Length];
        result.missileFireRatePoints = new int[baseAllocation.missileModuleSubType.Length];
        result.missileProjectileSpeedPoints = new int[baseAllocation.missileModuleSubType.Length];
        result.missileSilencePoints = new int[baseAllocation.missileModuleSubType.Length];
        ApplyModulesToSlots(result.missileModuleSubType, bodies.missiles);

        result.hangarModuleSubType = new string[baseAllocation.hangarModuleSubType.Length];
        result.hangarShipAttackPoints = new int[baseAllocation.hangarModuleSubType.Length];
        result.hangarFighterAttackPoints = new int[baseAllocation.hangarModuleSubType.Length];
        result.hangarAmmoPoints = new int[baseAllocation.hangarModuleSubType.Length];
        result.hangarHealthPoints = new int[baseAllocation.hangarModuleSubType.Length];
        ApplyModulesToSlots(result.hangarModuleSubType, bodies.hangars);

        return result;
    }

    private static void ApplyModulesToSlots(string[] subTypeArray, List<ModuleInfo> modules)
    {
        if (modules == null) return;
        for (int i = 0; i < modules.Count; i++)
        {
            int slotIndex = modules[i].slotIndex;
            if (slotIndex < 0 || slotIndex >= subTypeArray.Length) continue;
            subTypeArray[slotIndex] = modules[i].moduleSubType.ToString();
        }
    }
}
