// 함대 구성 — 지휘력 점유/반환(소모 아님), 리스트 기반 함선 배치, 전방/후방 설정
// Docs/Exploration_Revamp.md §1-2(지휘력), §1-3(리스트 기반 슬롯 관리) 참고
// 지휘력 최대치는 이 클래스가 조회하지 않음 — 세션/커맨더 데이터에서 이미 받아온 값을 생성자로 주입
using System.Collections.Generic;

public enum EFleetPlaceResult
{
    Success,
    NotEnoughCommandPower,
    HullNotFound,
}

public class FleetComposition
{
    // 강화 포인트 1당 소모 CP — DataTableConfig.gameSettings.general.reinforceCpCostPerPoint, 서버 GameDataService.getReinforceCpCostPerPoint()와 동일 소스
    public static int GetReinforceCpCostPerPoint()
    {
        return DataManager.Instance.m_dataTableConfig.gameSettings.general.reinforceCpCostPerPoint;
    }

    private int m_maxCommandPower;
    private List<FleetSlotEntry> m_placedShips = new();
    private DataTableModule m_moduleTable;

    public FleetComposition(int maxCommandPower, DataTableModule moduleTable)
    {
        m_maxCommandPower = maxCommandPower;
        m_moduleTable = moduleTable;
    }

    // 리스트 끝에 추가 — 순서가 상관없을 때 사용. modules를 생략하면(null) 무기 없는 빈 로드아웃으로 배치됨 —
    // 서버에서 이미 커스터마이징된 로드아웃을 복원할 때는 modules를 그대로 넘겨줌
    public EFleetPlaceResult TryPlaceShip(string hullSubType, bool isFront, ModuleHullInfo modules = null)
    {
        return TryPlaceShipAt(m_placedShips.Count, hullSubType, isFront, modules);
    }

    // 드래그로 특정 행 위치에 놓았을 때처럼, 삽입 위치를 지정해야 하는 경우 사용
    public EFleetPlaceResult TryPlaceShipAt(int index, string hullSubType, bool isFront, ModuleHullInfo modules = null)
    {
        ModuleData bodyData = m_moduleTable != null ? m_moduleTable.GetModuleDataFromTable(hullSubType) : null;
        if (bodyData == null)
        {
            return EFleetPlaceResult.HullNotFound;
        }

        ModuleHullInfo resolvedModules = modules != null ? modules : BuildDefaultModules();

        int clampedIndex = index < 0 ? 0 : index > m_placedShips.Count ? m_placedShips.Count : index;
        bool isReplacingExisting = clampedIndex < m_placedShips.Count;

        // 이미 함선이 배치된 슬롯에 드롭하면 교체 — 기존 함선의 지휘력을 먼저 반환한 뒤 새 비용을 체크
        int usedCommandPower = GetUsedCommandPower();
        if (isReplacingExisting == true)
        {
            usedCommandPower -= ComputeSlotCommandCost(m_placedShips[clampedIndex]);
        }

        int newShipCommandCost = ComputeSlotCommandCost(new FleetSlotEntry(hullSubType, isFront, resolvedModules));
        int remainingCommandPower = m_maxCommandPower - usedCommandPower;
        if (newShipCommandCost > remainingCommandPower)
        {
            return EFleetPlaceResult.NotEnoughCommandPower;
        }

        if (isReplacingExisting == true)
            m_placedShips[clampedIndex] = new FleetSlotEntry(hullSubType, isFront, resolvedModules);
        else
            m_placedShips.Insert(clampedIndex, new FleetSlotEntry(hullSubType, isFront, resolvedModules));
        return EFleetPlaceResult.Success;
    }

    // 빈 슬롯의 기본 로드아웃 — 무기 없이 배치
    private ModuleHullInfo BuildDefaultModules()
    {
        return new ModuleHullInfo { beams = new List<ModuleInfo>(), missiles = new List<ModuleInfo>(), hangars = new List<ModuleInfo>() };
    }

    // 바디 설치비 + 현재 장착된 모든 모듈의 설치비 합 — 서버 FleetService.computeSlotCommandCost와 동일 계산
    private int ComputeSlotCommandCost(FleetSlotEntry entry)
    {
        int bodyCost = 0;
        if (m_moduleTable != null)
        {
            ModuleData bodyData = m_moduleTable.GetModuleDataFromTable(entry.hullSubType);
            bodyCost = bodyData != null ? bodyData.statPoint : 0;
        }

        int modulesCost = 0;
        modulesCost += SumModuleCost(entry.modules != null ? entry.modules.beams : null);
        modulesCost += SumModuleCost(entry.modules != null ? entry.modules.missiles : null);
        modulesCost += SumModuleCost(entry.modules != null ? entry.modules.hangars : null);
        if (entry.modules != null)
        {
            modulesCost += ComputeSingleModuleCost(entry.modules.shieldModuleSubType, entry.modules.shieldGaugePoints + entry.modules.shieldRegenRatePoints);
            modulesCost += ComputeSingleModuleCost(entry.modules.interceptorModuleSubType, entry.modules.interceptorRegenRatePoints);
        }
        return bodyCost + modulesCost;
    }

    // 실드/요격체처럼 리스트가 아니라 서브타입 문자열 1개로 장착되는 모듈의 비용 — 미장착(빈 문자열)이면 0, 서버 computeShipCommandCost와 동일하게 설치비 + 강화 포인트 × CP
    private int ComputeSingleModuleCost(string moduleSubType, int reinforcePoints)
    {
        if (string.IsNullOrEmpty(moduleSubType) == true || m_moduleTable == null) return 0;

        ModuleData data = m_moduleTable.GetModuleDataFromTable(moduleSubType);
        int installCost = data != null ? data.statPoint : 0;
        return installCost + GetReinforceCpCostPerPoint() * reinforcePoints;
    }

    private int SumModuleCost(List<ModuleInfo> modules)
    {
        if (modules == null || m_moduleTable == null) return 0;
        int sum = 0;
        for (int i = 0; i < modules.Count; i++)
        {
            ModuleData data = m_moduleTable.GetModuleDataFromTable(modules[i].moduleSubType);
            int installCost = data != null ? data.statPoint : 0;
            int reinforceCost = GetReinforceCpCostPerPoint() * SumReinforcePoints(modules[i]);
            sum += installCost + reinforceCost;
        }
        return sum;
    }

    // 토글 API 응답 반영 — 해당 슬롯의 장착 모듈 상태만 갱신(hullSubType/isFront는 그대로)
    public void ApplyModuleToggleResult(int index, ModuleHullInfo modules)
    {
        if (index < 0 || index >= m_placedShips.Count) return;
        FleetSlotEntry entry = m_placedShips[index];
        entry.modules = modules;
        m_placedShips[index] = entry;
    }

    public ModuleHullInfo GetSlotModules(int index)
    {
        if (index < 0 || index >= m_placedShips.Count) return null;
        return m_placedShips[index].modules;
    }

    // 슬롯의 실제 현재 지휘력 코스트(바디 + 장착 모듈 합) — 정적 statPoint와 달리 토글로 추가/해제된 모듈까지 반영됨
    public int GetSlotCommandCost(int index)
    {
        if (index < 0 || index >= m_placedShips.Count) return 0;
        return ComputeSlotCommandCost(m_placedShips[index]);
    }

    // 기존 장착 모듈(existingModules) 중 새 함체(newHullSubType)에도 같은 카테고리+슬롯 인덱스가 존재하는 것만 남김 — 강화 포인트는 그대로 복사
    // (서버 FleetService.filterModulesForNewHull과 동일 규칙, 함체 변경 시 미리보기/실제 배치 양쪽에서 공용으로 사용)
    // 설계 확정: 무기 티어는 함체 티어와 독립적인 별도 축(강화로 별도 상승)이지만, 상한은 항상 함체 티어 — 함체를 더 낮은 티어로 바꾸면
    // 그 함체 티어를 넘는 기존 모듈은 함체 티어에 맞춰 자동 다운그레이드됨(지휘력 회수는 별도 처리 불필요 — 아래서 낮아진 statPoint로 재조립하므로 이후 비용 계산에 자동 반영)
    // existingModules가 null이면(원래 비어있던 슬롯의 신규 배치) null을 그대로 반환 — TryPlaceShipAt의 기본 로드아웃(BuildDefaultModules, 무기 없음) 분기를 그대로 타게 함
    public static ModuleHullInfo FilterModulesForNewHull(ModuleHullInfo existingModules, string newHullSubType)
    {
        if (existingModules == null) return null;

        int[] newMaxSlots = ParseMaxSlotsFromHullSubType(newHullSubType);
        int newHullTier = CommonUtility.ParseTier(newHullSubType);
        ModuleHullInfo result = new ModuleHullInfo { beams = new List<ModuleInfo>(), missiles = new List<ModuleInfo>(), hangars = new List<ModuleInfo>() };
        AppendKeptModules(result.beams, existingModules.beams, newMaxSlots[0], newHullTier);
        AppendKeptModules(result.missiles, existingModules.missiles, newMaxSlots[1], newHullTier);
        AppendKeptModules(result.hangars, existingModules.hangars, newMaxSlots[2], newHullTier);

        // 실드/요격체는 새 함체가 해당 슬롯을 지원할 때만 유지(서버 filterModulesForNewHull과 동일) — 티어는 함체 티어로 클램프, 포인트는 그대로 복사
        result.shieldModuleSubType = "";
        if (newMaxSlots[3] > 0 && string.IsNullOrEmpty(existingModules.shieldModuleSubType) == false)
        {
            result.shieldModuleSubType = ClampSubTypeToHullTier(EModuleType.shield, existingModules.shieldModuleSubType, newHullTier);
            result.shieldGaugePoints = existingModules.shieldGaugePoints;
            result.shieldRegenRatePoints = existingModules.shieldRegenRatePoints;
        }

        result.interceptorModuleSubType = "";
        if (newMaxSlots[4] > 0 && string.IsNullOrEmpty(existingModules.interceptorModuleSubType) == false)
        {
            result.interceptorModuleSubType = ClampSubTypeToHullTier(EModuleType.interceptor, existingModules.interceptorModuleSubType, newHullTier);
            result.interceptorRegenRatePoints = existingModules.interceptorRegenRatePoints;
        }
        return result;
    }

    private static void AppendKeptModules(List<ModuleInfo> target, List<ModuleInfo> source, int maxSlotCount, int newHullTier)
    {
        if (source == null) return;
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i].slotIndex >= maxSlotCount) continue;
            target.Add(ClampModuleTierToHull(source[i], newHullTier));
        }
    }

    // 모듈 티어가 함체 티어를 넘으면 {category}_{hullTier}_1로 다운그레이드 — 해당 티어 데이터가 없으면(비정상 데이터) 원본 유지
    private static ModuleInfo ClampModuleTierToHull(ModuleInfo module, int hullTier)
    {
        string clampedSubType = ClampSubTypeToHullTier(module.moduleType, module.moduleSubType, hullTier);
        if (clampedSubType == module.moduleSubType) return module;

        ModuleInfo clamped = CloneModuleInfo(module);
        clamped.moduleSubType = clampedSubType;
        return clamped;
    }

    // 편집용 복사본 — 서브타입/슬롯 정보와 모든 강화 포인트를 그대로 복제
    public static ModuleInfo CloneModuleInfo(ModuleInfo module)
    {
        return new ModuleInfo
        {
            moduleType = module.moduleType,
            moduleSubType = module.moduleSubType,
            moduleLevel = module.moduleLevel,
            hullIndex = module.hullIndex,
            slotIndex = module.slotIndex,
            attackPoints = module.attackPoints,
            attackToFighterPoints = module.attackToFighterPoints,
            fireRatePoints = module.fireRatePoints,
            silencePoints = module.silencePoints,
            ammoPoints = module.ammoPoints,
            healthPoints = module.healthPoints,
            disruptPoints = module.disruptPoints,
        };
    }

    public static bool AreSameReinforcePoints(ModuleInfo a, ModuleInfo b)
    {
        return a.attackPoints == b.attackPoints
            && a.attackToFighterPoints == b.attackToFighterPoints
            && a.fireRatePoints == b.fireRatePoints
            && a.silencePoints == b.silencePoints
            && a.ammoPoints == b.ammoPoints
            && a.healthPoints == b.healthPoints
            && a.disruptPoints == b.disruptPoints;
    }

    // 모듈 1개에 투자된 모든 강화 포인트의 합 — CP 비용 = 이 값 × reinforceCpCostPerPoint (서버 FleetService.sumReinforcePoints와 동일)
    public static int SumReinforcePoints(ModuleInfo module)
    {
        if (module == null) return 0;
        return module.attackPoints + module.attackToFighterPoints + module.fireRatePoints + module.silencePoints + module.ammoPoints + module.healthPoints + module.disruptPoints;
    }

    // 서브타입 티어가 함체 티어를 넘으면 {category}_{hullTier}_1로 다운그레이드한 이름을 반환 — 해당 티어 데이터가 없으면(비정상 데이터) 원본 그대로
    private static string ClampSubTypeToHullTier(EModuleType moduleType, string subType, int hullTier)
    {
        if (CommonUtility.ParseTier(subType) <= hullTier) return subType;

        string downgradedSubType = $"{moduleType}_{hullTier}_1";
        if (DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(downgradedSubType) == null) return subType;

        return downgradedSubType;
    }

    // 새 함체(newHullSubType) + 유지되는 모듈(keptModules) 기준 실제 지휘력 비용 미리보기 — ComputeSlotCommandCost와 동일 계산식 재사용
    public int ComputeProjectedSlotCommandCost(string newHullSubType, ModuleHullInfo keptModules)
    {
        return ComputeSlotCommandCost(new FleetSlotEntry(newHullSubType, false, keptModules));
    }

    // hullSubType(예: "hull_3_1_11100") → [beam, missile, hangar, shield, interceptor] 카테고리별 최대 슬롯 수 — 서버 GameDataService.parseMaxSlotsFromHullSubType과 동일 규칙
    // 이름 규칙: hull_{tier}_{gen}_{5자리구성} — 4번째 토큰(5자리)만 슬롯 수로 사용
    public static int[] ParseMaxSlotsFromHullSubType(string hullSubType)
    {
        return CommonUtility.ParseHullSlotComposition(hullSubType);
    }

    // 배치 해제 시 지휘력은 즉시 반환됨 (점유 구조, 소모 아님)
    public void RemoveShip(int index)
    {
        if (index < 0 || index >= m_placedShips.Count) return;
        m_placedShips.RemoveAt(index);
    }

    public void SetFront(int index, bool isFront)
    {
        if (index < 0 || index >= m_placedShips.Count) return;
        FleetSlotEntry entry = m_placedShips[index];
        entry.isFront = isFront;
        m_placedShips[index] = entry;
    }

    public int GetUsedCommandPower()
    {
        int used = 0;
        for (int i = 0; i < m_placedShips.Count; i++)
        {
            used += ComputeSlotCommandCost(m_placedShips[i]);
        }
        return used;
    }

    public int GetRemainingCommandPower()
    {
        return m_maxCommandPower - GetUsedCommandPower();
    }

    public int GetMaxCommandPower()
    {
        return m_maxCommandPower;
    }

    // IncreaseCommandPowerMax 응답 후 서버 확정값으로 즉시 반영 — 별도 재조회 없이 로컬 갱신
    public void SetMaxCommandPower(int maxCommandPower)
    {
        m_maxCommandPower = maxCommandPower;
    }

    public List<FleetSlotEntry> GetPlacedShips()
    {
        return m_placedShips;
    }

    private static readonly List<ModuleInfo> s_installedModuleBuffer = new();

    // 한 함선에 장착된 모듈을 빔 → 미사일 → 격납고 → 실드 → 요격체 순으로 buffer에 채움(실드/요격체는 서브타입 문자열 1개라 ModuleInfo로 변환)
    private static void CollectInstalledModules(ModuleHullInfo modules, List<ModuleInfo> buffer)
    {
        buffer.Clear();
        if (modules == null) return;

        if (modules.beams != null) buffer.AddRange(modules.beams);
        if (modules.missiles != null) buffer.AddRange(modules.missiles);
        if (modules.hangars != null) buffer.AddRange(modules.hangars);

        if (string.IsNullOrEmpty(modules.shieldModuleSubType) == false)
            buffer.Add(new ModuleInfo { moduleType = EModuleType.shield, slotIndex = 0, moduleSubType = modules.shieldModuleSubType });
        if (string.IsNullOrEmpty(modules.interceptorModuleSubType) == false)
            buffer.Add(new ModuleInfo { moduleType = EModuleType.interceptor, slotIndex = 0, moduleSubType = modules.interceptorModuleSubType });
    }

    // 배치된 모든 함선의 장착 모듈 중 티어 2 이상이 하나라도 있는지 — 티어업 경험 여부 판단(모듈은 항상 티어1로 장착됨)
    public bool HasTieredUpModule()
    {
        for (int i = 0; i < m_placedShips.Count; i++)
        {
            CollectInstalledModules(m_placedShips[i].modules, s_installedModuleBuffer);
            for (int j = 0; j < s_installedModuleBuffer.Count; j++)
            {
                if (CommonUtility.ParseTier(s_installedModuleBuffer[j].moduleSubType) >= 2) return true;
            }
        }
        return false;
    }

    // 지금 티어업이 실제로 가능한 첫 모듈 — 모듈 티어가 함체 티어보다 낮고, 여유 지휘력이 (다음 티어 statPoint - 현재 티어 statPoint) 이상
    // 함선 슬롯 순 → 빔/미사일/격납고/실드/요격체 순으로 탐색. categorySlotIndex는 카테고리 안에서의 슬롯 인덱스(실드/요격체는 0)
    public bool TryFindModuleTierUpCandidate(out int shipSlotIndex, out EModuleType moduleType, out int categorySlotIndex)
    {
        shipSlotIndex = -1;
        moduleType = EModuleType.beam;
        categorySlotIndex = 0;
        if (m_moduleTable == null) return false;

        int remainingCommandPower = GetRemainingCommandPower();
        for (int i = 0; i < m_placedShips.Count; i++)
        {
            int hullTier = CommonUtility.ParseTier(m_placedShips[i].hullSubType);
            CollectInstalledModules(m_placedShips[i].modules, s_installedModuleBuffer);
            for (int j = 0; j < s_installedModuleBuffer.Count; j++)
            {
                ModuleInfo module = s_installedModuleBuffer[j];
                int moduleTier = CommonUtility.ParseTier(module.moduleSubType);
                if (moduleTier >= hullTier) continue;

                ModuleData currentData = m_moduleTable.GetModuleDataFromTable(module.moduleSubType);
                ModuleData nextData = m_moduleTable.GetModuleDataFromTable($"{module.moduleType}_{moduleTier + 1}_1");
                if (currentData == null || nextData == null) continue;

                int tierUpCost = nextData.statPoint - currentData.statPoint;
                if (tierUpCost > remainingCommandPower) continue;

                shipSlotIndex = i;
                moduleType = module.moduleType;
                categorySlotIndex = module.slotIndex;
                return true;
            }
        }
        return false;
    }

    // 전투시작 요청(EnterExplorationCellRequest)에 실어 보낼 페이로드 변환
    public FleetInfo ToNetworkFleetInfo()
    {
        var fleetInfo = new FleetInfo();
        fleetInfo.ships = new List<ShipInfo>();
        for (int i = 0; i < m_placedShips.Count; i++)
        {
            FleetSlotEntry entry = m_placedShips[i];
            fleetInfo.ships.Add(new ShipInfo
            {
                // id/positionIndex는 이 리스트 내 자리(i)를 그대로 씀 — FleetComposition은 슬롯을 리스트 순서로만 관리하므로
                // 별도 슬롯 번호 개념이 없음. SpaceFleet 진형 배치(positionIndex 기준)와 서버 체력 스냅샷 매칭(positionIndex 기준)이
                // 함선마다 고유값을 필요로 해서 0(기본값) 그대로 두면 전원 충돌함
                id = i,
                positionIndex = i,
                hullSubType = entry.hullSubType,
                isFront = entry.isFront,
                hulls = entry.modules != null ? new List<ModuleHullInfo> { entry.modules } : null,
            });
        }
        return fleetInfo;
    }
}
