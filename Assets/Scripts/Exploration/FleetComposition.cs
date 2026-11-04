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
    private int m_maxCommandPower;
    private List<FleetSlotEntry> m_placedShips = new();
    private DataTableModule m_moduleTable;

    public FleetComposition(int maxCommandPower, DataTableModule moduleTable)
    {
        m_maxCommandPower = maxCommandPower;
        m_moduleTable = moduleTable;
    }

    // 리스트 끝에 추가 — 순서가 상관없을 때 사용. modules를 생략하면(null) 모든 구조 슬롯에 레벨1 모듈을 기본 장착한 상태로 배치됨 —
    // 서버에서 이미 커스터마이징된 로드아웃을 복원할 때는 modules를 그대로 넘겨줌
    public EFleetPlaceResult TryPlaceShip(string hullSubType, bool isFront, ModuleHullInfo modules = null)
    {
        return TryPlaceShipAt(m_placedShips.Count, hullSubType, isFront, modules);
    }

    // 상태를 바꾸지 않는 배치 사전검증 — 함체 존재 + 지휘력 충족만 판정.
    // 서버 응답이 성공한 뒤에야 로컬 편성을 바꾸는 호출부가 요청을 보내기 전 검증에 사용
    public EFleetPlaceResult CheckPlaceShipAt(int index, string hullSubType, bool isFront, ModuleHullInfo modules = null)
    {
        ModuleData bodyData = m_moduleTable != null ? m_moduleTable.GetModuleDataFromTable(hullSubType) : null;
        if (bodyData == null)
        {
            return EFleetPlaceResult.HullNotFound;
        }

        ModuleHullInfo resolvedModules = modules != null ? modules : BuildDefaultLevel1Modules(hullSubType);

        // 상한 클램프 없음 — 슬롯은 더 이상 꼬리에만 비지 않으므로, 리스트 길이보다 먼저인 인덱스도 그대로 허용(패딩은 TryPlaceShipAt에서 처리)
        int clampedIndex = index < 0 ? 0 : index;
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

        return EFleetPlaceResult.Success;
    }

    // 드래그로 특정 행 위치에 놓았을 때처럼, 삽입 위치를 지정해야 하는 경우 사용
    public EFleetPlaceResult TryPlaceShipAt(int index, string hullSubType, bool isFront, ModuleHullInfo modules = null)
    {
        EFleetPlaceResult checkResult = CheckPlaceShipAt(index, hullSubType, isFront, modules);
        if (checkResult != EFleetPlaceResult.Success)
        {
            return checkResult;
        }

        ModuleHullInfo resolvedModules = modules != null ? modules : BuildDefaultLevel1Modules(hullSubType);

        int clampedIndex = index < 0 ? 0 : index;
        bool isReplacingExisting = clampedIndex < m_placedShips.Count;

        if (isReplacingExisting == true)
        {
            m_placedShips[clampedIndex] = new FleetSlotEntry(hullSubType, isFront, resolvedModules);
        }
        else
        {
            // 목표 인덱스보다 리스트가 짧으면 그 사이를 빈 슬롯으로 채워서 늘림 — 중간에 빈 슬롯이 있어도 자리 유지
            while (m_placedShips.Count < clampedIndex)
                m_placedShips.Add(new FleetSlotEntry(null, true, null));
            m_placedShips.Add(new FleetSlotEntry(hullSubType, isFront, resolvedModules));
        }
        return EFleetPlaceResult.Success;
    }

    // 캐파(지휘력) 점유값은 바디(hull) 자신의 값만 — 모듈 설치/해제는 캐파와 무관(M13, 서버 FleetService.computeShipCommandCost와 동일 계산)
    private int ComputeSlotCommandCost(FleetSlotEntry entry)
    {
        if (m_moduleTable == null) return 0;

        ModuleData bodyData = m_moduleTable.GetModuleDataFromTable(entry.hullSubType);
        return bodyData != null ? bodyData.statPoint : 0;
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

    // 편집용 복사본 — 서브타입/슬롯 정보를 그대로 복제
    public static ModuleInfo CloneModuleInfo(ModuleInfo module)
    {
        return new ModuleInfo
        {
            moduleType = module.moduleType,
            moduleSubType = module.moduleSubType,
            moduleLevel = module.moduleLevel,
            hullIndex = module.hullIndex,
            slotIndex = module.slotIndex,
        };
    }

    // 새 함체(newHullSubType) 기준 실제 지휘력 비용 미리보기 — ComputeSlotCommandCost와 동일 계산식 재사용.
    // 캐파는 바디(hull) 자신의 값만 반영하므로(M13) keptModules는 비용 계산에 쓰이지 않음 — 미리보기 스탯 계산용으로만 호출부에서 넘김
    public int ComputeProjectedSlotCommandCost(string newHullSubType, ModuleHullInfo keptModules)
    {
        return ComputeSlotCommandCost(new FleetSlotEntry(newHullSubType, false, keptModules));
    }

    // hullSubType → [beam, missile, hangar, shield, interceptor] 카테고리별 최대 슬롯 수 — 아키타입 프리팹에서 파싱된
    // ModuleData.moduleSlots를 집계(서버 GameDataService.getMaxSlotsForHull과 동일 규칙/소스)
    public static int[] ParseMaxSlotsFromHullSubType(string hullSubType)
    {
        return CommonUtility.ParseHullSlotComposition(hullSubType);
    }

    // 아직 언락 안 된(미보유) 함체를 미리보기할 때 사용 — 모든 구조 슬롯에 레벨1 기본 모듈을 채운 ModuleHullInfo를 가상으로 조립
    // (서버 FleetService.buildDefaultModules와 동일한 "항상 설치된 상태" 기본값)
    public static ModuleHullInfo BuildDefaultLevel1Modules(string hullSubType)
    {
        int[] maxSlots = ParseMaxSlotsFromHullSubType(hullSubType);
        ModuleHullInfo hull = new ModuleHullInfo { beams = new List<ModuleInfo>(), missiles = new List<ModuleInfo>(), hangars = new List<ModuleInfo>(), shieldModuleSubType = "", interceptorModuleSubType = "" };

        AppendDefaultLevel1Modules(hull.beams, EModuleType.beam, maxSlots[0]);
        AppendDefaultLevel1Modules(hull.missiles, EModuleType.missile, maxSlots[1]);
        AppendDefaultLevel1Modules(hull.hangars, EModuleType.hangar, maxSlots[2]);
        if (maxSlots[3] > 0) hull.shieldModuleSubType = "shield_1_1";
        if (maxSlots[4] > 0) hull.interceptorModuleSubType = "interceptor_1_1";

        return hull;
    }

    private static void AppendDefaultLevel1Modules(List<ModuleInfo> target, EModuleType moduleType, int maxSlotCount)
    {
        string defaultSubType = $"{moduleType}_1_1";
        for (int i = 0; i < maxSlotCount; i++)
            target.Add(new ModuleInfo { moduleType = moduleType, slotIndex = i, moduleSubType = defaultSubType });
    }

    // 배치 해제 시 지휘력은 즉시 반환됨 (점유 구조, 소모 아님)
    // 자리는 유지 — 다른 슬롯이 밀려오지 않도록 리스트에서 빼지 않고(RemoveAt 금지) 그 자리를 빈 슬롯으로 비움
    public void RemoveShip(int index)
    {
        if (index < 0 || index >= m_placedShips.Count) return;
        m_placedShips[index] = new FleetSlotEntry(null, true, null);
    }

    public void SetFront(int index, bool isFront)
    {
        if (index < 0 || index >= m_placedShips.Count) return;
        FleetSlotEntry entry = m_placedShips[index];
        entry.isFront = isFront;
        m_placedShips[index] = entry;
    }

    public bool GetIsFront(int index)
    {
        if (index < 0 || index >= m_placedShips.Count) return true;
        return m_placedShips[index].isFront;
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

    // 커맨더 레벨업 등으로 최대치가 바뀌면 즉시 반영 — 별도 재조회 없이 로컬 갱신(M13, Commander.UpdateCommanderLevel에서 호출)
    public void SetMaxCommandPower(int maxCommandPower)
    {
        m_maxCommandPower = maxCommandPower;
    }

    public List<FleetSlotEntry> GetPlacedShips()
    {
        return m_placedShips;
    }

    // 전투시작 요청(EnterExplorationCellRequest)에 실어 보낼 페이로드 변환
    public FleetInfo ToNetworkFleetInfo()
    {
        var fleetInfo = new FleetInfo();
        fleetInfo.ships = new List<ShipInfo>();
        for (int i = 0; i < m_placedShips.Count; i++)
        {
            FleetSlotEntry entry = m_placedShips[i];
            if (string.IsNullOrEmpty(entry.hullSubType) == true) continue; // 빈 슬롯은 전송하지 않음

            fleetInfo.ships.Add(new ShipInfo
            {
                // id/positionIndex는 이 리스트 내 자리(i)를 그대로 씀 — 빈 슬롯이 중간에 있어도 압축하지 않고 그 인덱스 그대로 사용.
                // SpaceFleet 진형 배치(positionIndex 기준)와 서버 체력 스냅샷 매칭(positionIndex 기준)이
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
