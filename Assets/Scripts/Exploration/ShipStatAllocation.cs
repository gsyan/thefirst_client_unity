// 실제 장착된 함선 로드아웃(슬롯별 서브타입 + 강화 포인트) — 전투 계산용 중간 표현
// 장착 코스트/기본값 출처: DataTableConfig.gameSettings.shipStatFormula (ShipStatFormulaSettings)
// 카테고리별 슬롯 상한(maxModuleSlots)은 DataTableConfig에서 관리 — 이 클래스는 배열 길이 그대로 사용
// 슬롯 장착 여부는 별도 bool 배열로 관리(빈 문자열 = 미장착)
// Docs/Exploration_Revamp.md §1-1(장착+강화), §1-4(실드/요격체) 참고
using System.Collections.Generic;

[System.Serializable]
public class ShipStatAllocation
{
    // Beam — 슬롯당 장착 서브타입(모듈 서브타입 이름 문자열, 예: beam_1_1) + 속성별 강화 포인트(공격력/연사력). 빈 문자열 = 미장착
    public string[] beamModuleSubType = new string[0];
    public int[] beamAttackPoints = new int[0];
    public int[] beamFireRatePoints = new int[0];

    // Missile — 빔과 동일 구조 + 적중 시 대상 무장 침묵 시간 강화(미사일 전용)
    public string[] missileModuleSubType = new string[0];
    public int[] missileAttackPoints = new int[0];
    public int[] missileFireRatePoints = new int[0];
    public int[] missileSilencePoints = new int[0];

    // Hangar — 슬롯당 장착 서브타입 + 4개 서브스탯
    public string[] hangarModuleSubType = new string[0];
    public int[] hangarAttackToShip = new int[0];
    public int[] hangarAttackToFighter = new int[0];
    public int[] hangarAmmoPoints = new int[0];
    public int[] hangarHealthPoints = new int[0];
    public int[] hangarDisruptPoints = new int[0];

    // Interceptor — 슬롯당 장착 서브타입(예: interceptor_1_1) + 회복속도 강화 포인트. 빈 문자열 = 미장착. 장착 코스트는 DataTableModule의 해당 subType statPoint
    public string[] interceptorModuleSubType = new string[0];
    public int[] interceptorRegenRatePoints = new int[0];

    // Shield — 장착 서브타입(예: shield_t1_m1). 빈 문자열 = 미장착. 코스트는 DataTableModule의 해당 subType(level 1) cost_mp. 강화 서브스탯은 게이지/회복속도 중 1p=1선택
    public string shieldModuleSubType = "";
    public int shieldGaugePoints;
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
                total += GetInstallCost(moduleTable, beamModuleSubType[i]) + GetAt(beamAttackPoints, i) + GetAt(beamFireRatePoints, i);
        }

        for (int i = 0; i < missileModuleSubType.Length; i++)
        {
            if (string.IsNullOrEmpty(missileModuleSubType[i]) == false)
                total += GetInstallCost(moduleTable, missileModuleSubType[i]) + GetAt(missileAttackPoints, i) + GetAt(missileFireRatePoints, i) +GetAt(missileSilencePoints, i);
        }

        for (int i = 0; i < hangarModuleSubType.Length; i++)
        {
            if (string.IsNullOrEmpty(hangarModuleSubType[i]) == false)
                total += GetInstallCost(moduleTable, hangarModuleSubType[i]) + hangarAttackToShip[i] + hangarAttackToFighter[i] + hangarAmmoPoints[i] + hangarHealthPoints[i] + hangarDisruptPoints[i];
        }

        for (int i = 0; i < interceptorModuleSubType.Length; i++)
        {
            if (string.IsNullOrEmpty(interceptorModuleSubType[i]) == false)
                total += GetInstallCost(moduleTable, interceptorModuleSubType[i]) + interceptorRegenRatePoints[i];
        }

        if (string.IsNullOrEmpty(shieldModuleSubType) == false)
            total += GetInstallCost(moduleTable, shieldModuleSubType) + shieldGaugePoints + shieldRegenRatePoints;

        return total;
    }

    // 신규 추가 필드는 기존 로드아웃 데이터에서 배열 크기가 subType 배열과 다를 수 있어 범위를 벗어나면 0으로 취급
    private static int GetAt(int[] array, int index)
    {
        return index < array.Length ? array[index] : 0;
    }

    private static int GetInstallCost(DataTableModule moduleTable, string subTypeName)
    {
        if (moduleTable == null || string.IsNullOrEmpty(subTypeName) == true) return 0;
        ModuleData data = moduleTable.GetModuleDataFromTable(subTypeName);
        return data != null ? data.statPoint : 0;
    }

    // 실제 장착 로드아웃(ShipInfo.hulls, on/off + 공격력 강화 포인트 지원) → 전투 계산용 ShipStatAllocation 조립
    // maxSlotCount: 카테고리별 슬롯 배열 크기(DataTableConfig.gameSettings.shipStatFormula.maxModuleSlots) — 실제 장착 여부/서브타입/공격력 강화 포인트는 hulls로 채움
    // 침묵시간/탄약/체력은 강화 미지원이라 항상 0. 실드는 게이지/회복속도, 요격체는 회복속도 포인트를 hulls에서 읽음(요격체는 슬롯 없이 서브타입 1개만 장착, 인덱스 0)
    public static ShipStatAllocation BuildFromModuleHullInfo(int maxSlotCount, ModuleHullInfo hulls)
    {
        var result = new ShipStatAllocation();

        result.beamModuleSubType = new string[maxSlotCount];
        result.beamAttackPoints = new int[maxSlotCount];
        result.beamFireRatePoints = new int[maxSlotCount];
        ApplyModulesToSlots(result.beamModuleSubType, result.beamAttackPoints, result.beamFireRatePoints, hulls != null ? hulls.beams : null);

        result.missileModuleSubType = new string[maxSlotCount];
        result.missileAttackPoints = new int[maxSlotCount];
        result.missileFireRatePoints = new int[maxSlotCount];
        result.missileSilencePoints = new int[maxSlotCount];
        ApplyModulesToSlots(result.missileModuleSubType, result.missileAttackPoints, result.missileFireRatePoints, hulls != null ? hulls.missiles : null, result.missileSilencePoints);

        result.hangarModuleSubType = new string[maxSlotCount];
        result.hangarAttackToShip = new int[maxSlotCount];
        result.hangarAttackToFighter = new int[maxSlotCount];
        result.hangarAmmoPoints = new int[maxSlotCount];
        result.hangarHealthPoints = new int[maxSlotCount];
        result.hangarDisruptPoints = new int[maxSlotCount];
        ApplyHangarModulesToSlots(result.hangarModuleSubType, result.hangarAttackToShip, result.hangarAttackToFighter, result.hangarAmmoPoints, result.hangarHealthPoints,
            result.hangarDisruptPoints, hulls != null ? hulls.hangars : null);

        result.shieldModuleSubType = hulls != null && string.IsNullOrEmpty(hulls.shieldModuleSubType) == false ? hulls.shieldModuleSubType : "";
        result.shieldGaugePoints = hulls != null ? hulls.shieldGaugePoints : 0;
        result.shieldRegenRatePoints = hulls != null ? hulls.shieldRegenRatePoints : 0;

        result.interceptorModuleSubType = new string[maxSlotCount];
        result.interceptorRegenRatePoints = new int[maxSlotCount];
        if (hulls != null && string.IsNullOrEmpty(hulls.interceptorModuleSubType) == false && maxSlotCount > 0)
        {
            result.interceptorModuleSubType[0] = hulls.interceptorModuleSubType;
            result.interceptorRegenRatePoints[0] = hulls.interceptorRegenRatePoints;
        }

        return result;
    }

    // 빔/미사일 공용 — 공격력/연사력 강화 포인트 반영. 침묵 포인트 배열은 미사일만 넘김(빔은 null)
    private static void ApplyModulesToSlots(string[] subTypeArray, int[] attackPointsArray, int[] fireRatePointsArray, List<ModuleInfo> modules, int[] silencePointsArray = null)
    {
        if (modules == null) return;
        for (int i = 0; i < modules.Count; i++)
        {
            int slotIndex = modules[i].slotIndex;
            if (slotIndex < 0 || slotIndex >= subTypeArray.Length) continue;
            subTypeArray[slotIndex] = modules[i].moduleSubType;
            attackPointsArray[slotIndex] = modules[i].attackPoints;
            fireRatePointsArray[slotIndex] = modules[i].fireRatePoints;
            if (silencePointsArray != null) silencePointsArray[slotIndex] = modules[i].silencePoints;
        }
    }

    // 격납고 전용 — ModuleInfo의 attackPoints(대함)/attackToFighterPoints(대전투기)/ammoPoints/healthPoints/disruptPoints를 각각의 배열에 반영
    private static void ApplyHangarModulesToSlots(string[] subTypeArray, int[] attackToShipArray, int[] attackToFighterArray, int[] ammoArray, int[] healthArray, int[] disruptArray, List<ModuleInfo> modules)
    {
        if (modules == null) return;
        for (int i = 0; i < modules.Count; i++)
        {
            int slotIndex = modules[i].slotIndex;
            if (slotIndex < 0 || slotIndex >= subTypeArray.Length) continue;
            subTypeArray[slotIndex] = modules[i].moduleSubType;
            attackToShipArray[slotIndex] = modules[i].attackPoints;
            attackToFighterArray[slotIndex] = modules[i].attackToFighterPoints;
            ammoArray[slotIndex] = modules[i].ammoPoints;
            healthArray[slotIndex] = modules[i].healthPoints;
            disruptArray[slotIndex] = modules[i].disruptPoints;
        }
    }
}
