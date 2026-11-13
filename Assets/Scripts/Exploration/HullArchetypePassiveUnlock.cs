using System.Collections.Generic;

// 실드/요격체 사용 가능 여부 — 아키타입 패시브 단계의 해금 효과(unlock_*)가 완료됐을 때만 사용 가능(서버 HullArchetypeProgressService.isModuleUnlocked와 같은 규칙)
// 함체에 슬롯이 있는지(has_shield/has_interceptor)는 호출 측이 판단 — 슬롯 없는 함체는 애초에 실드/요격체가 없음
public static class HullArchetypePassiveUnlock
{
    private const string k_effectUnlockShield = "unlock_shield";
    private const string k_effectUnlockInterceptor = "unlock_interceptor";
    private const string k_effectUnlockShieldInterceptor = "unlock_shield_interceptor";

    // 실드/요격체가 아닌 모듈은 항상 true
    public static bool IsModuleUnlocked(string hullArchetype, EModuleType moduleType)
    {
        if (moduleType != EModuleType.shield && moduleType != EModuleType.interceptor) return true;

        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander == null) return false;

        List<HullArchetypePassiveData> passiveList = DataManager.Instance.m_dataTableHullArchetypePassive.GetPassiveDataList();
        for (int i = 0; i < passiveList.Count; i++)
        {
            HullArchetypePassiveData passive = passiveList[i];
            if (passive.hullArchetype != hullArchetype) continue;
            if (IsUnlockEffectFor(passive.effectType, moduleType) == false) continue;

            commander.GetHullArchetypePassiveStageProgress(passive.hullArchetype, passive.stage, passive.threshold, out long counterValue, out bool completed);
            if (completed == true) return true;
        }
        return false;
    }

    // 이 모듈을 해금하는 패시브 단계 번호 — 해금 효과가 데이터에 없으면 0(잠금 문구에 단계 표시용)
    public static int GetUnlockStage(string hullArchetype, EModuleType moduleType)
    {
        List<HullArchetypePassiveData> passiveList = DataManager.Instance.m_dataTableHullArchetypePassive.GetPassiveDataList();
        for (int i = 0; i < passiveList.Count; i++)
        {
            HullArchetypePassiveData passive = passiveList[i];
            if (passive.hullArchetype != hullArchetype) continue;
            if (IsUnlockEffectFor(passive.effectType, moduleType) == true) return passive.stage;
        }
        return 0;
    }

    private static bool IsUnlockEffectFor(string effectType, EModuleType moduleType)
    {
        if (effectType == k_effectUnlockShieldInterceptor) return true;
        if (moduleType == EModuleType.shield) return effectType == k_effectUnlockShield;
        if (moduleType == EModuleType.interceptor) return effectType == k_effectUnlockInterceptor;
        return false;
    }
}
