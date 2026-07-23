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

    public FleetComposition(int maxCommandPower, Dictionary<string, ShipPresetData> presetTable)
    {
        m_maxCommandPower = maxCommandPower;
        m_presetTable = presetTable;
    }

    // 리스트 끝에 추가 — 순서가 상관없을 때 사용
    public EFleetPlaceResult TryPlaceShip(string shipPresetId, bool isFront)
    {
        return TryPlaceShipAt(m_placedShips.Count, shipPresetId, isFront);
    }

    // 드래그로 특정 행 위치에 놓았을 때처럼, 삽입 위치를 지정해야 하는 경우 사용
    public EFleetPlaceResult TryPlaceShipAt(int index, string shipPresetId, bool isFront)
    {
        ShipPresetData presetData;
        bool found = m_presetTable.TryGetValue(shipPresetId, out presetData);
        if (found == false)
        {
            return EFleetPlaceResult.PresetNotFound;
        }

        int usedCommandPower = GetUsedCommandPower();
        int remainingCommandPower = m_maxCommandPower - usedCommandPower;
        if (presetData.commandCost > remainingCommandPower)
        {
            return EFleetPlaceResult.NotEnoughCommandPower;
        }

        int clampedIndex = index < 0 ? 0 : index > m_placedShips.Count ? m_placedShips.Count : index;
        m_placedShips.Insert(clampedIndex, new FleetSlotEntry(shipPresetId, isFront));
        return EFleetPlaceResult.Success;
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
            ShipPresetData presetData;
            bool found = m_presetTable.TryGetValue(m_placedShips[i].shipPresetId, out presetData);
            if (found)
            {
                used += presetData.commandCost;
            }
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

    public List<FleetSlotEntry> GetPlacedShips()
    {
        return m_placedShips;
    }

    // 전투시작 요청(EnterExplorationCellRequest)에 실어 보낼 페이로드 변환
    public TempFleetInfo ToNetworkFleetInfo()
    {
        var fleetInfo = new TempFleetInfo();
        fleetInfo.ships = new List<TempShipInfo>();
        for (int i = 0; i < m_placedShips.Count; i++)
        {
            FleetSlotEntry entry = m_placedShips[i];
            fleetInfo.ships.Add(new TempShipInfo
            {
                shipPresetId = entry.shipPresetId,
                isFront = entry.isFront,
            });
        }
        return fleetInfo;
    }
}
