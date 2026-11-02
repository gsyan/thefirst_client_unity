// 실제 장착된 함선 로드아웃(슬롯별 서브타입) — 전투 계산용 중간 표현
// 장착 코스트/기본값 출처: DataTableConfig.gameSettings.shipStatFormula (ShipStatFormulaSettings)
// 카테고리별 슬롯 상한(maxModuleSlots)은 DataTableConfig에서 관리 — 이 클래스는 배열 길이 그대로 사용
// 슬롯 장착 여부는 별도 bool 배열로 관리(빈 문자열 = 미장착)
using System.Collections.Generic;

[System.Serializable]
public class ShipStatAllocation
{
    // Beam/Missile — 슬롯당 장착 서브타입(모듈 서브타입 이름 문자열, 예: beam_1_1). 빈 문자열 = 미장착
    public string[] beamModuleSubType = new string[0];
    public string[] missileModuleSubType = new string[0];

    // Hangar — 슬롯당 장착 서브타입
    public string[] hangarModuleSubType = new string[0];

    // Interceptor — 슬롯당 장착 서브타입(예: interceptor_1_1). 빈 문자열 = 미장착
    public string[] interceptorModuleSubType = new string[0];

    // Shield — 장착 서브타입(예: shield_t1_m1). 빈 문자열 = 미장착
    public string shieldModuleSubType = "";

    // 실제 장착 로드아웃(ShipInfo.hulls, on/off 지원) → 전투 계산용 ShipStatAllocation 조립
    // maxSlotCount: 카테고리별 슬롯 배열 크기(DataTableConfig.gameSettings.shipStatFormula.maxModuleSlots) — 실제 장착 여부/서브타입은 hulls로 채움
    // 요격체는 슬롯 없이 서브타입 1개만 장착(인덱스 0)
    public static ShipStatAllocation BuildFromModuleHullInfo(int maxSlotCount, ModuleHullInfo hulls)
    {
        var result = new ShipStatAllocation();

        result.beamModuleSubType = new string[maxSlotCount];
        ApplyModulesToSlots(result.beamModuleSubType, hulls != null ? hulls.beams : null);

        result.missileModuleSubType = new string[maxSlotCount];
        ApplyModulesToSlots(result.missileModuleSubType, hulls != null ? hulls.missiles : null);

        result.hangarModuleSubType = new string[maxSlotCount];
        ApplyModulesToSlots(result.hangarModuleSubType, hulls != null ? hulls.hangars : null);

        result.shieldModuleSubType = hulls != null && string.IsNullOrEmpty(hulls.shieldModuleSubType) == false ? hulls.shieldModuleSubType : "";

        result.interceptorModuleSubType = new string[maxSlotCount];
        if (hulls != null && string.IsNullOrEmpty(hulls.interceptorModuleSubType) == false && maxSlotCount > 0)
            result.interceptorModuleSubType[0] = hulls.interceptorModuleSubType;

        return result;
    }

    private static void ApplyModulesToSlots(string[] subTypeArray, List<ModuleInfo> modules)
    {
        if (modules == null) return;
        for (int i = 0; i < modules.Count; i++)
        {
            int slotIndex = modules[i].slotIndex;
            if (slotIndex < 0 || slotIndex >= subTypeArray.Length) continue;
            subTypeArray[slotIndex] = modules[i].moduleSubType;
        }
    }
}
