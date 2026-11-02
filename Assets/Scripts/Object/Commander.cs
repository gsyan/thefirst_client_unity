// 플레이어 커맨더(캐릭터) 상태 관리
public class Commander
{
    public CommanderInfo m_commanderInfo;

    // 완료했지만 아직 안 받은 업적 존재 여부 — 서버에서 안 내려오면(로그인 직후 등) false로 시작, 존런 정산/업적 패널 응답으로 갱신됨(레드닷 표시용)
    private bool m_hasUnclaimedAchievement;

    // 오늘 수령 가능한 출석 보상이 남아있는지 — 로그인 시점 DailyBonusManager.CheckDailyBonusStatus() 응답으로 갱신됨(레드닷 표시용)
    private bool m_hasUnclaimedDailyBonus;

    public Commander(CommanderInfo commanderInfo)
    {
        m_commanderInfo = commanderInfo;
    }

    // "commander_" 로 시작하면 로컬라이즈된 이름 + commanderId 반환
    public static string GetDisplayName(string rawName, long commanderId)
    {
        if (string.IsNullOrEmpty(rawName) || rawName.StartsWith("commander_", System.StringComparison.OrdinalIgnoreCase))
            return LocalizationManager.Instance.Get("UI_Commander") + commanderId;
        return rawName;
    }

    public string GetName()
    {
        if (m_commanderInfo == null) return GetDisplayName("", 0);
        return GetDisplayName(m_commanderInfo.commanderName, m_commanderInfo.commanderId);
    }

    public int GetExp()
    {
        if (m_commanderInfo == null) return 0;
        return m_commanderInfo.exp;
    }

    public void UpdateExp(int exp)
    {
        if (m_commanderInfo == null) return;
        m_commanderInfo.exp = exp;
        EventManager.TriggerCommanderExpChanged(exp);
    }

    public int GetPvpPoint()
    {
        if (m_commanderInfo == null) return 0;
        return m_commanderInfo.pvpPoint;
    }

    public int GetPvpPointMaxGot()
    {
        if (m_commanderInfo == null) return 0;
        return m_commanderInfo.pvpPointMaxGot;
    }

    public void UpdatePvpPoint(int pvpPoint)
    {
        if (m_commanderInfo == null) return;
        m_commanderInfo.pvpPoint = pvpPoint;
        EventManager.TriggerPvpPointChanged(pvpPoint);
    }

    public void UpdatePvpPointMaxGot(int pvpPointMaxGot)
    {
        if (m_commanderInfo == null) return;
        m_commanderInfo.pvpPointMaxGot = pvpPointMaxGot;
    }

    public int GetExplorationPoint()
    {
        if (m_commanderInfo == null) return 0;
        return m_commanderInfo.explorationPoint;
    }

    public void UpdateExplorationPoint(int explorationPoint)
    {
        if (m_commanderInfo == null) return;
        m_commanderInfo.explorationPoint = explorationPoint;
        EventManager.TriggerExplorationPointChanged(explorationPoint);
    }

    public int GetTacticPower()
    {
        if (m_commanderInfo == null) return 0;
        return m_commanderInfo.tacticPower;
    }

    // 전술 비용 차감 — 여유가 있는지(GetTacticPower() >= cost)는 호출부 책임, 여기선 차감과 이벤트 발생만 담당
    public void SpendTacticPower(int cost)
    {
        if (m_commanderInfo == null) return;
        m_commanderInfo.tacticPower -= cost;
        EventManager.Trigger_TacticPowerChanged(m_commanderInfo.tacticPower, m_commanderInfo.tacticPowerMax);
    }

    // 최대 전술력 대비 healRatio만큼 가산 회복(즉시효과 보상카드용)
    public void HealTacticPowerByRatio(float healRatio)
    {
        if (healRatio <= 0f || m_commanderInfo == null) return;
        m_commanderInfo.tacticPower = UnityEngine.Mathf.Min(m_commanderInfo.tacticPowerMax, m_commanderInfo.tacticPower + UnityEngine.Mathf.RoundToInt(m_commanderInfo.tacticPowerMax * healRatio));
        EventManager.Trigger_TacticPowerChanged(m_commanderInfo.tacticPower, m_commanderInfo.tacticPowerMax);
    }

    public int GetMineralAmount()
    {
        if (m_commanderInfo == null) return 0;
        return m_commanderInfo.mineralAmount;
    }

    // 자원 게이지(전술력) 자동/패시브 회복 없음 — 존런 중 쌓인 광물을 소모해서만 수동 충전 가능.
    // 광물 1개당 mineralToResourceRatio만큼 자원을 채우며, 자원이 이미 가득 차 있으면(필요량 0) 광물을 소모하지 않고 false 반환
    public bool ChargeTacticPowerWithMineral()
    {
        if (m_commanderInfo == null) return false;

        int neededPoints = m_commanderInfo.tacticPowerMax - m_commanderInfo.tacticPower;
        if (neededPoints <= 0 || m_commanderInfo.mineralAmount <= 0) return false;

        int ratio = DataManager.Instance.m_dataTableConfig.gameSettings.tactic.mineralToResourceRatio;
        if (ratio <= 0) return false;

        int affordableMineral = (neededPoints + ratio - 1) / ratio; // 필요 포인트를 다 채우는 데 드는 광물 수(올림)
        int mineralToSpend = UnityEngine.Mathf.Min(affordableMineral, m_commanderInfo.mineralAmount);
        int gainedPoints = UnityEngine.Mathf.Min(neededPoints, mineralToSpend * ratio);

        m_commanderInfo.mineralAmount -= mineralToSpend;
        m_commanderInfo.tacticPower += gainedPoints;

        EventManager.Trigger_TacticPowerChanged(m_commanderInfo.tacticPower, m_commanderInfo.tacticPowerMax);
        EventManager.Trigger_MineralAmountChanged(m_commanderInfo.mineralAmount);
        return true;
    }

    public int GetAchievementPoint()
    {
        if (m_commanderInfo == null) return 0;
        return m_commanderInfo.achievementPoint;
    }

    public void UpdateAchievementPoint(int achievementPoint)
    {
        if (m_commanderInfo == null) return;
        m_commanderInfo.achievementPoint = achievementPoint;
        EventManager.TriggerAchievementPointChanged(achievementPoint);
    }

    public bool GetHasUnclaimedAchievement()
    {
        return m_hasUnclaimedAchievement;
    }

    // 값이 실제로 바뀔 때만 이벤트 발행 — 존런 정산/업적 패널 열기/받기 등 여러 갱신 지점에서 같은 상태로 중복 호출돼도 스팸 안 남
    public void UpdateHasUnclaimedAchievement(bool hasUnclaimedAchievement)
    {
        if (m_hasUnclaimedAchievement == hasUnclaimedAchievement) return;
        m_hasUnclaimedAchievement = hasUnclaimedAchievement;
        EventManager.TriggerUnclaimedAchievementChanged(hasUnclaimedAchievement);
    }

    public bool GetHasUnclaimedDailyBonus()
    {
        return m_hasUnclaimedDailyBonus;
    }

    // 값이 실제로 바뀔 때만 이벤트 발행(위 업적과 동일한 패턴)
    public void UpdateHasUnclaimedDailyBonus(bool hasUnclaimedDailyBonus)
    {
        if (m_hasUnclaimedDailyBonus == hasUnclaimedDailyBonus) return;
        m_hasUnclaimedDailyBonus = hasUnclaimedDailyBonus;
        EventManager.TriggerUnclaimedDailyBonusChanged(hasUnclaimedDailyBonus);
    }

    public bool IsHullUnlocked(string hullSubType)
    {
        if (m_commanderInfo == null || m_commanderInfo.unlockedHulls == null) return false;
        return m_commanderInfo.unlockedHulls.Contains(hullSubType);
    }

    // 업적포인트로 "직접" 언락한 함체가 하나라도 있는지 — 티어1은 커맨더 생성 시점에 서버가 기본으로 심어두는 무료 기록이라 카운트에서 제외
    public bool HasAnyUnlockedHull()
    {
        if (m_commanderInfo == null || m_commanderInfo.unlockedHulls == null) return false;
        return m_commanderInfo.unlockedHulls.Exists(h => CommonUtility.ParseTier(h) > 1);
    }

    // UnlockHull API 성공 응답의 unlockedHulls(권위값)로 갱신 — 호출부(UIHullPickerView)가 직접 화면을 다시 그림
    public void UpdateUnlockedHulls(System.Collections.Generic.List<string> unlockedHulls)
    {
        if (m_commanderInfo == null) return;
        m_commanderInfo.unlockedHulls = unlockedHulls;
    }

    public int GetCommanderLevel()
    {
        if (m_commanderInfo == null) return 1;
        int level = m_commanderInfo.commanderLevel;
        return level > 0 ? level : 1;
    }

    // 서버 응답 commanderLevel로 갱신 후 이벤트 발생 — 지휘력(캐파) 최대치도 레벨에 맞춰 로컬 재계산(M13, 구매 없이 레벨로만 결정)
    public void UpdateCommanderLevel(int newLevel)
    {
        if (m_commanderInfo == null) return;
        m_commanderInfo.commanderLevel = newLevel;

        DataTableCommander dataTableCommander = DataManager.Instance.m_dataTableCommander;
        if (dataTableCommander != null)
        {
            int newCommandPowerMax = dataTableCommander.GetCommandPower(newLevel);
            m_commanderInfo.commandPowerMax = newCommandPowerMax;

            FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
            if (composition != null)
                composition.SetMaxCommandPower(newCommandPowerMax);
        }

        EventManager.TriggerCommanderLevelChange(newLevel);
    }

    public CommanderInfo GetInfo()
    {
        return m_commanderInfo;
    }

    public void UpdateCommanderInfo(CommanderInfo commanderInfo)
    {
        m_commanderInfo = commanderInfo;
        UpdateHasUnclaimedAchievement(commanderInfo.hasUnclaimedAchievement);
    }

    public void UpdateCommanderName(string commanderName, int nameChangeCount)
    {
        if (m_commanderInfo == null) return;
        m_commanderInfo.commanderName = commanderName;
        m_commanderInfo.nameChangeCount = nameChangeCount;
    }

    public bool CheckEnoughExp(long cost)
    {
        if (m_commanderInfo == null) return false;
        return m_commanderInfo.exp >= cost;
    }

    // 아키타입 단위 영구 모듈강화(M6) 현재 레벨 — 기록이 없으면 0. beam/missile/hangar는 slotIndex별로 독립, hull/shield/interceptor는 항상 slotIndex=0
    public int GetArchetypeUpgradeLevel(string hullArchetype, EModuleType moduleType, int slotIndex = 0)
    {
        if (m_commanderInfo == null || m_commanderInfo.archetypeUpgrades == null) return 0;
        for (int i = 0; i < m_commanderInfo.archetypeUpgrades.Count; i++)
        {
            ArchetypeUpgradeInfo entry = m_commanderInfo.archetypeUpgrades[i];
            if (entry.hullArchetype == hullArchetype && entry.moduleType == moduleType && entry.slotIndex == slotIndex)
                return entry.level;
        }
        return 0;
    }

    // UpgradeArchetypeModule API 성공 응답(권위값)으로 로컬 캐시 갱신 — 없으면 새로 추가, 있으면 레벨만 갱신
    public void UpdateArchetypeUpgradeLevel(string hullArchetype, EModuleType moduleType, int slotIndex, int level)
    {
        if (m_commanderInfo == null) return;
        if (m_commanderInfo.archetypeUpgrades == null)
            m_commanderInfo.archetypeUpgrades = new System.Collections.Generic.List<ArchetypeUpgradeInfo>();

        for (int i = 0; i < m_commanderInfo.archetypeUpgrades.Count; i++)
        {
            if (m_commanderInfo.archetypeUpgrades[i].hullArchetype == hullArchetype && m_commanderInfo.archetypeUpgrades[i].moduleType == moduleType
                && m_commanderInfo.archetypeUpgrades[i].slotIndex == slotIndex)
            {
                ArchetypeUpgradeInfo updated = m_commanderInfo.archetypeUpgrades[i];
                updated.level = level;
                m_commanderInfo.archetypeUpgrades[i] = updated;
                return;
            }
        }

        m_commanderInfo.archetypeUpgrades.Add(new ArchetypeUpgradeInfo { hullArchetype = hullArchetype, moduleType = moduleType, slotIndex = slotIndex, level = level });
    }

    // 아키타입 패시브(1~5성, 4단계) 언락 조건 누적 카운터 — 기록이 없으면 0
    public long GetHullArchetypePassiveCounterValue(string hullArchetype, string counterType)
    {
        if (m_commanderInfo == null || m_commanderInfo.hullArchetypePassiveProgress == null) return 0;
        for (int i = 0; i < m_commanderInfo.hullArchetypePassiveProgress.Count; i++)
        {
            HullArchetypeProgressInfo entry = m_commanderInfo.hullArchetypePassiveProgress[i];
            if (entry.hullArchetype == hullArchetype && entry.counterType == counterType)
                return entry.counterValue;
        }
        return 0;
    }

}
