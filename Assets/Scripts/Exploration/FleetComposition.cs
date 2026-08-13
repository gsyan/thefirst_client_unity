// 함대 구성 — 지휘력 점유/반환(소모 아님), 리스트 기반 함선 배치, 전방/후방 설정
// Docs/Exploration_Revamp.md §1-2(지휘력), §1-3(리스트 기반 슬롯 관리) 참고
// 지휘력 최대치는 이 클래스가 조회하지 않음 — 세션/커맨더 데이터에서 이미 받아온 값을 생성자로 주입
using System.Collections.Generic;

public enum EFleetPlaceResult
{
    Success,
    NotEnoughCommandPower,
    PresetNotFound,
}

public class FleetComposition
{
    private int m_maxCommandPower;
    private List<FleetSlotEntry> m_placedShips = new();
    private Dictionary<string, ShipPresetData> m_presetTable;
    private DataTableModule m_moduleTable;

    public FleetComposition(int maxCommandPower, Dictionary<string, ShipPresetData> presetTable, DataTableModule moduleTable)
    {
        m_maxCommandPower = maxCommandPower;
        m_presetTable = presetTable;
        m_moduleTable = moduleTable;
    }

    // 리스트 끝에 추가 — 순서가 상관없을 때 사용. modules를 생략하면(null) 프리셋의 기본 로드아웃(빔1)으로 시딩됨 —
    // 서버에서 이미 커스터마이징된 로드아웃을 복원할 때는 modules를 그대로 넘겨줌
    public EFleetPlaceResult TryPlaceShip(string shipPresetId, bool isFront, ModuleBodyInfo modules = null)
    {
        return TryPlaceShipAt(m_placedShips.Count, shipPresetId, isFront, modules);
    }

    // 드래그로 특정 행 위치에 놓았을 때처럼, 삽입 위치를 지정해야 하는 경우 사용
    public EFleetPlaceResult TryPlaceShipAt(int index, string shipPresetId, bool isFront, ModuleBodyInfo modules = null)
    {
        ShipPresetData presetData;
        bool found = m_presetTable.TryGetValue(shipPresetId, out presetData);
        if (found == false)
        {
            return EFleetPlaceResult.PresetNotFound;
        }

        ModuleBodyInfo resolvedModules = modules != null ? modules : BuildDefaultModules(presetData);

        int clampedIndex = index < 0 ? 0 : index > m_placedShips.Count ? m_placedShips.Count : index;
        bool isReplacingExisting = clampedIndex < m_placedShips.Count;

        // 이미 함선이 배치된 슬롯에 드롭하면 교체 — 기존 함선의 지휘력을 먼저 반환한 뒤 새 비용을 체크
        int usedCommandPower = GetUsedCommandPower();
        if (isReplacingExisting == true)
        {
            usedCommandPower -= ComputeSlotCommandCost(m_placedShips[clampedIndex]);
        }

        int newShipCommandCost = ComputeSlotCommandCost(new FleetSlotEntry(shipPresetId, isFront, resolvedModules));
        int remainingCommandPower = m_maxCommandPower - usedCommandPower;
        if (newShipCommandCost > remainingCommandPower)
        {
            return EFleetPlaceResult.NotEnoughCommandPower;
        }

        if (isReplacingExisting == true)
            m_placedShips[clampedIndex] = new FleetSlotEntry(shipPresetId, isFront, resolvedModules);
        else
            m_placedShips.Insert(clampedIndex, new FleetSlotEntry(shipPresetId, isFront, resolvedModules));
        return EFleetPlaceResult.Success;
    }

    // presetData.statAllocation에서 비어있지 않은 슬롯(현재는 beam slot0=beam_t1)만 추출 — modules_in_preset.csv/서버 defaultModules와 동일 규칙
    private ModuleBodyInfo BuildDefaultModules(ShipPresetData presetData)
    {
        var body = new ModuleBodyInfo { beams = new List<ModuleInfo>(), missiles = new List<ModuleInfo>(), hangars = new List<ModuleInfo>() };
        ShipStatAllocation alloc = presetData.statAllocation;
        if (alloc == null) return body;

        AppendDefaultModules(body.beams, EModuleType.beam, alloc.beamModuleSubType);
        AppendDefaultModules(body.missiles, EModuleType.missile, alloc.missileModuleSubType);
        AppendDefaultModules(body.hangars, EModuleType.hangar, alloc.hangarModuleSubType);
        return body;
    }

    private void AppendDefaultModules(List<ModuleInfo> target, EModuleType moduleType, string[] subTypeArray)
    {
        if (subTypeArray == null) return;
        for (int i = 0; i < subTypeArray.Length; i++)
        {
            if (string.IsNullOrEmpty(subTypeArray[i])) continue;
            if (System.Enum.TryParse(subTypeArray[i], out EModuleSubType subType) == false) continue;
            target.Add(new ModuleInfo { moduleType = moduleType, moduleSubType = subType, slotIndex = i });
        }
    }

    // 바디 설치비 + 현재 장착된 모든 모듈의 설치비 합 — 서버 FleetService.computeSlotCommandCost와 동일 계산
    private int ComputeSlotCommandCost(FleetSlotEntry entry)
    {
        ShipPresetData presetData;
        if (m_presetTable.TryGetValue(entry.shipPresetId, out presetData) == false) return 0;

        int bodyCost = 0;
        if (m_moduleTable != null && System.Enum.TryParse(presetData.prefabName, out EModuleSubType bodySubType))
        {
            ModuleData bodyData = m_moduleTable.GetModuleDataFromTable(bodySubType);
            bodyCost = bodyData != null ? bodyData.statPoint : 0;
        }

        int modulesCost = 0;
        modulesCost += SumModuleCost(entry.modules != null ? entry.modules.beams : null);
        modulesCost += SumModuleCost(entry.modules != null ? entry.modules.missiles : null);
        modulesCost += SumModuleCost(entry.modules != null ? entry.modules.hangars : null);
        return bodyCost + modulesCost;
    }

    private int SumModuleCost(List<ModuleInfo> modules)
    {
        if (modules == null || m_moduleTable == null) return 0;
        int sum = 0;
        for (int i = 0; i < modules.Count; i++)
        {
            ModuleData data = m_moduleTable.GetModuleDataFromTable(modules[i].moduleSubType);
            sum += data != null ? data.statPoint : 0;
        }
        return sum;
    }

    // 토글 API 응답 반영 — 해당 슬롯의 장착 모듈 상태만 갱신(shipPresetId/isFront는 그대로)
    public void ApplyModuleToggleResult(int index, ModuleBodyInfo modules)
    {
        if (index < 0 || index >= m_placedShips.Count) return;
        FleetSlotEntry entry = m_placedShips[index];
        entry.modules = modules;
        m_placedShips[index] = entry;
    }

    public ModuleBodyInfo GetSlotModules(int index)
    {
        if (index < 0 || index >= m_placedShips.Count) return null;
        return m_placedShips[index].modules;
    }

    // 슬롯의 실제 현재 지휘력 코스트(바디 + 장착 모듈 합) — 정적 presetData.commandCost와 달리 토글로 추가/해제된 모듈까지 반영됨
    public int GetSlotCommandCost(int index)
    {
        if (index < 0 || index >= m_placedShips.Count) return 0;
        return ComputeSlotCommandCost(m_placedShips[index]);
    }

    // presetId(예: "m11100") → [beam, missile, hangar, shield, interceptor] 카테고리별 최대 슬롯 수 — 서버 FleetService.parseMaxSlotsFromPresetId와 동일 규칙
    public static int[] ParseMaxSlotsFromPresetId(string presetId)
    {
        int[] result = new int[5];
        if (string.IsNullOrEmpty(presetId) || presetId.Length != 6 || presetId[0] != 'm') return result;
        for (int i = 0; i < 5; i++)
        {
            char c = presetId[1 + i];
            if (char.IsDigit(c) == false) return new int[5];
            result[i] = c - '0';
        }
        return result;
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
                shipPresetId = entry.shipPresetId,
                isFront = entry.isFront,
                bodies = entry.modules != null ? new List<ModuleBodyInfo> { entry.modules } : null,
            });
        }
        return fleetInfo;
    }
}
