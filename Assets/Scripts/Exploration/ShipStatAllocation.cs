// 실제 장착된 함선 로드아웃(슬롯별 서브타입 + 강화 포인트) — 전투 계산용 중간 표현
// 장착 코스트/기본값 출처: DataTableConfig.gameSettings.shipStatFormula (ShipStatFormulaSettings)
// 카테고리별 슬롯 상한(maxModuleSlots)은 DataTableConfig에서 관리 — 이 클래스는 배열 길이 그대로 사용
// 슬롯 장착 여부는 별도 bool 배열로 관리(빈 문자열 = 미장착)
// Docs/Exploration_Revamp.md §1-1(장착+강화), §1-4(실드/요격체) 참고
using System.Collections.Generic;

[System.Serializable]
public class ShipStatAllocation
{
    // Beam — 슬롯당 장착 서브타입(EModuleSubType 이름, 예: beam1) + 속성별 강화 포인트(공격력/연사력/발사체속도). 빈 문자열 = 미장착
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
    public int[] hangarAttackToShip = new int[0];
    public int[] hangarAttackToFighter = new int[0];
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
    // bodyPrefabName: 함체(prefabName) 자체의 설치 비용도 지휘력에 포함시키기 위한 body subType 이름(예: h1_11100). 생략 시 0으로 취급
    public int GetTotalPointsUsed(DataTableModule moduleTable, string bodyPrefabName = "")
    {
        int total = 0;
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
                total += GetInstallCost(moduleTable, hangarModuleSubType[i]) + hangarAttackToShip[i] + hangarAttackToFighter[i] + hangarAmmoPoints[i] + hangarHealthPoints[i];
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

    // 실제 장착 로드아웃(ShipInfo.bodies, on/off + 공격력 강화 포인트 지원) → 전투 계산용 ShipStatAllocation 조립
    // maxSlotCount: 카테고리별 슬롯 배열 크기(DataTableConfig.gameSettings.shipStatFormula.maxModuleSlots) — 실제 장착 여부/서브타입/공격력 강화 포인트는 bodies로 채움
    // 공격력 이외(연사력/발사체속도/침묵시간 등)는 아직 실시간 강화 미지원이라 항상 0. 실드/요격체는 아직 장착 UI가 없어 항상 미장착
    public static ShipStatAllocation BuildFromModuleBodyInfo(int maxSlotCount, ModuleBodyInfo bodies)
    {
        var result = new ShipStatAllocation();

        result.beamModuleSubType = new string[maxSlotCount];
        result.beamAttackPoints = new int[maxSlotCount];
        result.beamFireRatePoints = new int[maxSlotCount];
        result.beamProjectileSpeedPoints = new int[maxSlotCount];
        ApplyModulesToSlots(result.beamModuleSubType, result.beamAttackPoints, bodies != null ? bodies.beams : null);

        result.missileModuleSubType = new string[maxSlotCount];
        result.missileAttackPoints = new int[maxSlotCount];
        result.missileFireRatePoints = new int[maxSlotCount];
        result.missileProjectileSpeedPoints = new int[maxSlotCount];
        result.missileSilencePoints = new int[maxSlotCount];
        ApplyModulesToSlots(result.missileModuleSubType, result.missileAttackPoints, bodies != null ? bodies.missiles : null);

        result.hangarModuleSubType = new string[maxSlotCount];
        result.hangarAttackToShip = new int[maxSlotCount];
        result.hangarAttackToFighter = new int[maxSlotCount];
        result.hangarAmmoPoints = new int[maxSlotCount];
        result.hangarHealthPoints = new int[maxSlotCount];
        ApplyHangarModulesToSlots(result.hangarModuleSubType, result.hangarAttackToShip, result.hangarAttackToFighter, bodies != null ? bodies.hangars : null);

        result.interceptorModuleSubType = new string[maxSlotCount];
        result.interceptorDelayPoints = new int[maxSlotCount];
        result.interceptorRegenRatePoints = new int[maxSlotCount];

        return result;
    }

    // 빔/미사일 공용 — 공격력 강화 포인트 1개 필드만 반영(연사력/발사체속도/침묵시간은 아직 실시간 강화 미지원이라 배열이 전부 0으로 남음)
    private static void ApplyModulesToSlots(string[] subTypeArray, int[] attackPointsArray, List<ModuleInfo> modules)
    {
        if (modules == null) return;
        for (int i = 0; i < modules.Count; i++)
        {
            int slotIndex = modules[i].slotIndex;
            if (slotIndex < 0 || slotIndex >= subTypeArray.Length) continue;
            subTypeArray[slotIndex] = modules[i].moduleSubType.ToString();
            attackPointsArray[slotIndex] = modules[i].attackPoints;
        }
    }

    // 격납고 전용 — 대함/대전투기 공격력이 별도 축이라 ModuleInfo의 attackPoints(대함)/attackToFighterPoints(대전투기)를 각각의 배열에 반영
    private static void ApplyHangarModulesToSlots(string[] subTypeArray, int[] attackToShipArray, int[] attackToFighterArray, List<ModuleInfo> modules)
    {
        if (modules == null) return;
        for (int i = 0; i < modules.Count; i++)
        {
            int slotIndex = modules[i].slotIndex;
            if (slotIndex < 0 || slotIndex >= subTypeArray.Length) continue;
            subTypeArray[slotIndex] = modules[i].moduleSubType.ToString();
            attackToShipArray[slotIndex] = modules[i].attackPoints;
            attackToFighterArray[slotIndex] = modules[i].attackToFighterPoints;
        }
    }
}
